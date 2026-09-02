using Application.Interfaces.Notifications;
using Core.Entities;
using Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

/// <summary>
/// Write side of the notification feed (Plan §4.2 [SPEC]/[DECISION]).
///
/// NotifyRequestEventAsync only calls db.Notifications.Add(...) — it never calls
/// SaveChangesAsync itself. It's always invoked from inside RequestService, which is about to
/// call SaveChangesAsync on the same scoped DataContext anyway, so the notification rows
/// commit atomically with the status change they're reporting on, per the Plan's explicit
/// instruction not to use SaveChanges interceptors or a domain-event bus. Forgetting to save
/// afterward would silently drop the notification — that's a caller bug, not a case this
/// class defends against.
///
/// NotifyPasswordChangedAsync, MarkReadAsync and MarkAllReadAsync all save immediately —
/// see NotifyPasswordChangedAsync's own doc comment for why that one specifically can't join
/// an existing transaction the way the request events do.
/// </summary>
public class NotificationService(DataContext db) : INotificationService
{
    public Task NotifyRequestEventAsync(NotificationEventType eventType, Request request, int actorEmployeeNumber)
    {
        var (title, message) = BuildRequestNotification(eventType, request);

        // Dual-party per the source spec's "popped up to the person and his superior": in this
        // app's hierarchy model, "the person" is always the requestor and "his superior" is
        // the approver (Request.ApproverEmployeeNumber is literally set from the requestor's
        // SuperiorEmployeeNumber at creation — see RequestService.CreateAsync). That pairing
        // is used regardless of which of the two actually performed the action, so an approver
        // approving still notifies the requestor, not the approver's own manager.
        var recipients = new HashSet<int> { request.RequestorEmployeeNumber };
        if (request.ApproverEmployeeNumber.HasValue)
        {
            recipients.Add(request.ApproverEmployeeNumber.Value);
        }

        var now = DateTime.UtcNow;
        foreach (var recipient in recipients)
        {
            db.Notifications.Add(new Notification
            {
                RecipientEmployeeNumber = recipient,
                RequestId = request.Id,
                EventType = eventType,
                Title = title,
                Message = message,
                IsRead = false,
                CreatedAtUtc = now,
            });
        }

        return Task.CompletedTask;
    }

    public async Task NotifyPasswordChangedAsync(int employeeNumber)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == employeeNumber);
        if (user is null)
        {
            // AuthService already verified the account exists before changing the password;
            // this is only a defensive guard against a race, not an expected path.
            return;
        }

        var now = DateTime.UtcNow;
        db.Notifications.Add(new Notification
        {
            RecipientEmployeeNumber = employeeNumber,
            RequestId = null,
            EventType = NotificationEventType.PasswordChanged,
            Title = "Password Changed",
            Message = "Your password was changed.",
            IsRead = false,
            CreatedAtUtc = now,
        });

        // Skipped, not a 0-row failure, if the actor has no superior (e.g. the Managing
        // Director) — there is nobody left to notify.
        if (user.SuperiorEmployeeNumber.HasValue)
        {
            db.Notifications.Add(new Notification
            {
                RecipientEmployeeNumber = user.SuperiorEmployeeNumber.Value,
                RequestId = null,
                EventType = NotificationEventType.PasswordChanged,
                Title = "Password Changed",
                Message = $"{user.Name}'s password was changed.",
                IsRead = false,
                CreatedAtUtc = now,
            });
        }

        await db.SaveChangesAsync();
    }

    public async Task<bool> MarkReadAsync(long notificationId, int ownerEmployeeNumber)
    {
        var notification = await db.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.RecipientEmployeeNumber == ownerEmployeeNumber);

        if (notification is null)
        {
            return false;
        }

        notification.IsRead = true;
        await db.SaveChangesAsync();
        return true;
    }

    public async Task MarkAllReadAsync(int employeeNumber)
    {
        var unread = await db.Notifications
            .Where(n => n.RecipientEmployeeNumber == employeeNumber && !n.IsRead)
            .ToListAsync();

        foreach (var notification in unread)
        {
            notification.IsRead = true;
        }

        if (unread.Count > 0)
        {
            await db.SaveChangesAsync();
        }
    }

    private static (string Title, string Message) BuildRequestNotification(NotificationEventType eventType, Request request) =>
        eventType switch
        {
            NotificationEventType.RequestSubmitted =>
                ("Request Submitted", $"Request #{request.Id} was submitted for approval."),
            NotificationEventType.RequestApproved => request.Status == "PartiallyApproved"
                ? ("Request Partially Approved", $"Request #{request.Id} was partially approved.")
                : ("Request Approved", $"Request #{request.Id} was approved."),
            NotificationEventType.RequestRejected =>
                ("Request Rejected", $"Request #{request.Id} was rejected."),
            NotificationEventType.RequestWithdrawn =>
                ("Request Withdrawn", $"Request #{request.Id} was withdrawn."),
            NotificationEventType.RequestCancelled =>
                ("Request Cancelled", $"Request #{request.Id} was cancelled."),
            NotificationEventType.PasswordChanged => throw new ArgumentException(
                "PasswordChanged is not a request event — use NotifyPasswordChangedAsync instead.",
                nameof(eventType)),
            _ => throw new ArgumentOutOfRangeException(nameof(eventType), eventType, null),
        };
}
