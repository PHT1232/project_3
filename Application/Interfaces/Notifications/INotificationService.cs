using Core.Entities;
using Core.Enums;

namespace Application.Interfaces.Notifications;

/// <summary>
/// Write side of the notification feed (Plan §4.2 [DECISION]: a single NotifyAsync-style
/// call, invoked inside the same transaction as the state change it's reporting on — never
/// via SaveChanges interceptors or a domain-event bus).
///
/// NotifyRequestEventAsync/NotifyPasswordChangedAsync deliberately do NOT call
/// SaveChangesAsync — they only stage rows on the shared, scoped DbContext. The caller's own
/// SaveChangesAsync (already committing the status change / password change) is what makes
/// the notification rows atomic with the thing they're reporting on. Calling this method and
/// then never saving is a bug, not a valid use.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Fires one of the 5 request-related triggers (submitted/approved/rejected/withdrawn/
    /// cancelled). Recipients are always the request's requestor and approver — see
    /// NotificationService's doc comment for why that pairing was chosen over a literal
    /// "actor and their superior" reading.
    /// </summary>
    Task NotifyRequestEventAsync(NotificationEventType eventType, Request request, int actorEmployeeNumber);

    /// <summary>
    /// Fires the 6th trigger (password changed). Recipients are the user themselves and
    /// their superior (looked up here, since AuthService/Application has no direct DB
    /// access) — skipped for the second row if the user has no superior (e.g. the MD).
    ///
    /// Unlike NotifyRequestEventAsync, this DOES call SaveChangesAsync itself: the password
    /// change already went through ASP.NET Core Identity's UserManager, which persists via
    /// its own SaveChangesAsync call before this method ever runs, so there's no still-open
    /// unit of work left to join. A failed notification write here never rolls back an
    /// already-successful password change, which is the correct behavior anyway.
    /// </summary>
    Task NotifyPasswordChangedAsync(int employeeNumber);

    /// <summary>Marks one notification read. Returns false if it doesn't exist or isn't owned by the caller.</summary>
    Task<bool> MarkReadAsync(long notificationId, int ownerEmployeeNumber);

    /// <summary>Marks every unread notification for this user as read.</summary>
    Task MarkAllReadAsync(int employeeNumber);
}
