using Application.DTOs.Common;
using Application.DTOs.Requests;
using Application.Exceptions;
using Application.Interfaces.Notifications;
using Application.Interfaces.Requests;
using Core.Entities;
using Core.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

/// <summary>
/// Request lifecycle service: create, approve/reject, withdraw, cancel, and list.
///
/// All mutations are transactional and include concurrency control (RowVersion compare-then-set).
/// Status changes write StatusHistory rows, trigger notifications, and run eligibility checks.
/// Stock movements are deferred to fulfillment (M4+).
///
/// Lives in Infrastructure (not Application) because it needs DataContext for atomic
/// multi-entity commits (status + history + notifications, all or nothing).
///
/// Every notificationService.NotifyRequestEventAsync(...) call below only stages rows —
/// it commits atomically with the rest of the method's changes on the same
/// db.SaveChangesAsync() call, per INotificationService's contract.
/// </summary>
public class RequestService(
    DataContext db,
    IRequestQueries queries,
    INotificationService notificationService,
    IValidator<CreateRequestCommand> createValidator,
    IValidator<ApproveRequestCommand> approveValidator,
    IValidator<WithdrawRequestCommand> withdrawValidator,
    IValidator<RequestCancellationCommand> requestCancelValidator) : IRequestService
{
    public async Task<RequestDto> CreateAsync(CreateRequestCommand command, int requestorEmployeeNumber)
    {
        // Validate command structure
        await createValidator.ValidateAndThrowAsync(command);

        // Load requestor and resolve approver
        var requestor = await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == requestorEmployeeNumber && u.IsActive)
            ?? throw new NotFoundException($"Requestor {requestorEmployeeNumber} not found or inactive.");

        // Approver is the requestor's superior (up to the caller to enforce role-based access).
        //
        // A requestor at the top of the hierarchy has no superior, so there is nobody who could
        // ever approve the request. Creating one anyway left it Pending with a null approver,
        // which every /approvals/{id}/approve call then rejected with 404 — a request permanently
        // stuck in Pending. page-map.md §5 ("New Request", server-side guards) states the rule:
        // "the MD (no superior) cannot raise a request (Plan [ASK] #11 default)".
        var approverEmployeeNumber = requestor.SuperiorEmployeeNumber
            ?? throw new ValidationException(
            [
                new FluentValidation.Results.ValidationFailure(
                    "requestorEmployeeNumber",
                    "You have no superior to approve this request, so it cannot be raised."),
            ]);

        _ = await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == approverEmployeeNumber && u.IsActive)
            ?? throw new NotFoundException($"Approver {approverEmployeeNumber} not found or inactive.");

        // Load and validate items
        var requestedItemIds = command.Items.Select(i => i.ItemId).ToList();
        var items = await db.StationeryItems
            .AsNoTracking()
            .Where(i => requestedItemIds.Contains(i.Id) && i.IsActive)
            .ToListAsync();

        var missingIds = requestedItemIds.Except(items.Select(i => i.Id)).ToList();
        if (missingIds.Any())
        {
            throw new ConflictException($"Items {string.Join(", ", missingIds)} not found or inactive.");
        }

        // Check requestor's rank eligibility for each item
        foreach (var lineItem in command.Items)
        {
            var item = items.FirstOrDefault(i => i.Id == lineItem.ItemId)
                ?? throw new NotFoundException($"Item {lineItem.ItemId} not found.");

            if (item.MinRankLevelToRequest > requestor.RankLevel)
            {
                throw new ConflictException(
                    $"Item {item.ItemName} requires rank level {item.MinRankLevelToRequest}, " +
                    $"but your rank is {requestor.RankLevel}.");
            }
        }

        // Build request with line items
        var requestLines = new List<RequestItem>();
        var totalCost = 0m;

        foreach (var lineInput in command.Items)
        {
            var item = items.First(i => i.Id == lineInput.ItemId);
            var lineTotal = lineInput.Quantity * item.UnitCost;
            totalCost += lineTotal;

            requestLines.Add(new RequestItem
            {
                ItemId = item.Id,
                Quantity = lineInput.Quantity,
                UnitCostSnapshot = item.UnitCost,
                LineTotal = lineTotal
            });
        }

        // Create request in Pending status
        var request = new Request
        {
            RequestorEmployeeNumber = requestorEmployeeNumber,
            ApproverEmployeeNumber = approverEmployeeNumber,
            Status = "Pending",
            TotalEstimatedCost = totalCost,
            RequiredByDate = command.RequiredByDate,
            CreatedAtUtc = DateTime.UtcNow,
            RowVersion = Guid.NewGuid(),
            Items = requestLines,
            StatusHistory = new List<RequestStatusHistory>
            {
                new()
                {
                    FromStatus = null,
                    ToStatus = "Pending",
                    ActorEmployeeNumber = requestorEmployeeNumber,
                    Comment = "Request created",
                    CreatedAtUtc = DateTime.UtcNow
                }
            }
        };

        await db.Requests.AddAsync(request);
        await db.SaveChangesAsync();

        return await queries.GetByIdAsync(request.Id, requestorEmployeeNumber)
            ?? throw new NotFoundException("Request not found after creation.");
    }

    public async Task<RequestDto> SubmitAsync(int requestId, Guid rowVersion, int submitterEmployeeNumber)
    {
        var request = await db.Requests
            .Include(r => r.Items)
            .FirstOrDefaultAsync(r => r.Id == requestId)
            ?? throw new NotFoundException($"Request {requestId} not found.");

        // Ownership check
        if (request.RequestorEmployeeNumber != submitterEmployeeNumber)
        {
            throw new NotFoundException("Request not accessible.");
        }

        // Concurrency check
        if (request.RowVersion != rowVersion)
        {
            throw new ConflictException("Request was modified. Please refresh and try again.");
        }

        // Only allow submit from Pending status
        if (request.Status != "Pending")
        {
            throw new ConflictException($"Cannot submit a request in {request.Status} status.");
        }

        // Add status history and update RowVersion
        request.StatusHistory.Add(new RequestStatusHistory
        {
            FromStatus = "Pending",
            ToStatus = "Pending",
            ActorEmployeeNumber = submitterEmployeeNumber,
            Comment = "Request submitted for approval",
            CreatedAtUtc = DateTime.UtcNow
        });

        request.RowVersion = Guid.NewGuid();
        await notificationService.NotifyRequestEventAsync(NotificationEventType.RequestSubmitted, request, submitterEmployeeNumber);
        await db.SaveChangesAsync();

        return await queries.GetByIdAsync(requestId, submitterEmployeeNumber)
            ?? throw new NotFoundException("Request not found after submission.");
    }

    public async Task<RequestDto> ApproveAsync(ApproveRequestCommand command, int approverEmployeeNumber)
    {
        await approveValidator.ValidateAndThrowAsync(command);

        var request = await db.Requests
            .Include(r => r.Items)
            .FirstOrDefaultAsync(r => r.Id == command.RequestId)
            ?? throw new NotFoundException($"Request {command.RequestId} not found.");

        // Approver check
        if (request.ApproverEmployeeNumber != approverEmployeeNumber)
        {
            throw new NotFoundException("You are not the approver for this request.");
        }

        // Status check
        if (request.Status != "Pending")
        {
            throw new ConflictException($"Cannot approve a request in {request.Status} status.");
        }

        // Concurrency check
        if (request.RowVersion != command.RowVersion)
        {
            throw new ConflictException("Request was modified. Please refresh and try again.");
        }

        // Validate line decisions match the request items
        if (command.LineDecisions.Count != request.Items.Count)
        {
            throw new ConflictException("Line decision count does not match request items.");
        }

        // Determine overall status based on decisions
        var approvedCount = command.LineDecisions.Count(d => d.Decision == "approved");
        var rejectedCount = command.LineDecisions.Count(d => d.Decision == "rejected");
        var modifiedCount = command.LineDecisions.Count(d => d.Decision == "modified");

        var newStatus = approvedCount == command.LineDecisions.Count ? "Approved"
            : rejectedCount == command.LineDecisions.Count ? "Rejected"
            : "PartiallyApproved";

        request.Status = newStatus;
        request.DecisionComment = command.Comment;
        request.DecidedAtUtc = DateTime.UtcNow;
        request.RowVersion = Guid.NewGuid();

        request.StatusHistory.Add(new RequestStatusHistory
        {
            FromStatus = "Pending",
            ToStatus = newStatus,
            ActorEmployeeNumber = approverEmployeeNumber,
            Comment = command.Comment,
            CreatedAtUtc = DateTime.UtcNow
        });

        var approvalEventType = newStatus == "Rejected"
            ? NotificationEventType.RequestRejected
            : NotificationEventType.RequestApproved;
        await notificationService.NotifyRequestEventAsync(approvalEventType, request, approverEmployeeNumber);
        await db.SaveChangesAsync();

        return await queries.GetByIdAsync(command.RequestId, approverEmployeeNumber)
            ?? throw new NotFoundException("Request not found after approval.");
    }

    public async Task<RequestDto> WithdrawAsync(int requestId, Guid rowVersion, int requestorEmployeeNumber)
    {
        var withdrawCommand = new WithdrawRequestCommand(requestId, rowVersion);
        await withdrawValidator.ValidateAndThrowAsync(withdrawCommand);

        var request = await db.Requests
            .Include(r => r.Items)
            .FirstOrDefaultAsync(r => r.Id == requestId)
            ?? throw new NotFoundException($"Request {requestId} not found.");

        // Ownership check
        if (request.RequestorEmployeeNumber != requestorEmployeeNumber)
        {
            throw new NotFoundException("Request not accessible.");
        }

        // Status check: only Pending can be withdrawn
        if (request.Status != "Pending")
        {
            throw new ConflictException($"Cannot withdraw a request in {request.Status} status.");
        }

        // Concurrency check
        if (request.RowVersion != rowVersion)
        {
            throw new ConflictException("Request was modified. Please refresh and try again.");
        }

        request.Status = "Withdrawn";
        request.RowVersion = Guid.NewGuid();

        request.StatusHistory.Add(new RequestStatusHistory
        {
            FromStatus = "Pending",
            ToStatus = "Withdrawn",
            ActorEmployeeNumber = requestorEmployeeNumber,
            Comment = "Request withdrawn by requestor",
            CreatedAtUtc = DateTime.UtcNow
        });

        await notificationService.NotifyRequestEventAsync(NotificationEventType.RequestWithdrawn, request, requestorEmployeeNumber);
        await db.SaveChangesAsync();

        return await queries.GetByIdAsync(requestId, requestorEmployeeNumber)
            ?? throw new NotFoundException("Request not found after withdrawal.");
    }

    public async Task<RequestDto> RequestCancellationAsync(
        int requestId, Guid rowVersion, int requestorEmployeeNumber, string? reason)
    {
        var cancelCommand = new RequestCancellationCommand(requestId, rowVersion, reason);
        await requestCancelValidator.ValidateAndThrowAsync(cancelCommand);

        var request = await db.Requests
            .Include(r => r.Items)
            .FirstOrDefaultAsync(r => r.Id == requestId)
            ?? throw new NotFoundException($"Request {requestId} not found.");

        // Ownership check
        if (request.RequestorEmployeeNumber != requestorEmployeeNumber)
        {
            throw new NotFoundException("Request not accessible.");
        }

        // Status check: only Approved or PartiallyApproved can request cancellation
        if (request.Status != "Approved" && request.Status != "PartiallyApproved")
        {
            throw new ConflictException(
                $"Cannot request cancellation for a request in {request.Status} status.");
        }

        // Concurrency check
        if (request.RowVersion != rowVersion)
        {
            throw new ConflictException("Request was modified. Please refresh and try again.");
        }

        var previousStatus = request.Status;
        request.Status = "CancellationPending";
        request.RowVersion = Guid.NewGuid();

        request.StatusHistory.Add(new RequestStatusHistory
        {
            FromStatus = previousStatus,
            ToStatus = "CancellationPending",
            ActorEmployeeNumber = requestorEmployeeNumber,
            Comment = reason ?? "Cancellation requested",
            CreatedAtUtc = DateTime.UtcNow
        });

        // No notification here by design — Plan §4.2 names exactly 6 triggers, and "cancelled"
        // is one of them, not "cancellation requested". The notification fires from
        // ApproveCancellationAsync below, only on the final Cancelled outcome.
        await db.SaveChangesAsync();

        return await queries.GetByIdAsync(requestId, requestorEmployeeNumber)
            ?? throw new NotFoundException("Request not found after cancellation request.");
    }

    public async Task<RequestDto> ApproveCancellationAsync(
        int requestId, Guid rowVersion, int approverEmployeeNumber, bool approved, string? reason)
    {
        var request = await db.Requests
            .Include(r => r.Items)
            .FirstOrDefaultAsync(r => r.Id == requestId)
            ?? throw new NotFoundException($"Request {requestId} not found.");

        // Approver check
        if (request.ApproverEmployeeNumber != approverEmployeeNumber)
        {
            throw new NotFoundException("You are not the approver for this request.");
        }

        // Status check: only CancellationPending
        if (request.Status != "CancellationPending")
        {
            throw new ConflictException($"Cannot respond to cancellation for a request in {request.Status} status.");
        }

        // Concurrency check
        if (request.RowVersion != rowVersion)
        {
            throw new ConflictException("Request was modified. Please refresh and try again.");
        }

        var newStatus = approved ? "Cancelled" : request.StatusHistory
            .Where(h => h.ToStatus != "CancellationPending" && h.ToStatus != "Withdrawn")
            .OrderByDescending(h => h.CreatedAtUtc)
            .FirstOrDefault()?.ToStatus ?? "Approved";

        request.Status = newStatus;
        request.RowVersion = Guid.NewGuid();

        request.StatusHistory.Add(new RequestStatusHistory
        {
            FromStatus = "CancellationPending",
            ToStatus = newStatus,
            ActorEmployeeNumber = approverEmployeeNumber,
            Comment = reason,
            CreatedAtUtc = DateTime.UtcNow
        });

        // Only the final "Cancelled" outcome is one of the Plan's 6 named triggers — a denial
        // (reverting to Approved/PartiallyApproved) doesn't fire a notification.
        if (approved)
        {
            await notificationService.NotifyRequestEventAsync(NotificationEventType.RequestCancelled, request, approverEmployeeNumber);
        }
        await db.SaveChangesAsync();

        return await queries.GetByIdAsync(requestId, approverEmployeeNumber)
            ?? throw new NotFoundException("Request not found after cancellation decision.");
    }

    public async Task<bool> DeletePendingAsync(int requestId, int requestorEmployeeNumber)
    {
        var request = await db.Requests
            .FirstOrDefaultAsync(r => r.Id == requestId)
            ?? throw new NotFoundException($"Request {requestId} not found.");

        // Ownership check
        if (request.RequestorEmployeeNumber != requestorEmployeeNumber)
        {
            throw new NotFoundException("Request not accessible.");
        }

        // Only pending (not yet submitted) can be deleted
        if (request.Status != "Pending")
        {
            return false;
        }

        db.Requests.Remove(request);
        await db.SaveChangesAsync();

        return true;
    }
}
