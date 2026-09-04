using Application.DTOs.Common;
using Application.DTOs.Requests;
using Application.Exceptions;
using Application.Interfaces.Notifications;
using Application.Interfaces.Requests;
using Application.Interfaces.Users;
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
    IEligibilityQueries eligibilityQueries,
    IValidator<CreateRequestCommand> createValidator,
    IValidator<ApproveRequestCommand> approveValidator,
    IValidator<WithdrawRequestCommand> withdrawValidator,
    IValidator<RequestCancellationCommand> requestCancelValidator,
    IValidator<ApproveCancellationCommand> approveCancelValidator) : IRequestService
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

        // Rank comes from the requestor's ROLE (AspNetRoles.RankLevel), which is the authoritative
        // source used by IdentityAccountAdapter, the JWT rankLevel claim, and the catalogue's
        // role filter in ItemQueries. ApplicationUser.RankLevel is a separate, unmaintained column
        // that is 1 for every seeded user — reading it here meant a Manager could SEE a rank-2 item
        // in the catalogue but got "requires rank level 2, but your rank is 1" when requesting it.
        var requestorRankLevel = await (
            from userRole in db.UserRoles
            join role in db.Roles on userRole.RoleId equals role.Id
            where userRole.UserId == requestorEmployeeNumber
            select role.RankLevel).FirstOrDefaultAsync();

        // Check requestor's rank eligibility for each item
        foreach (var lineItem in command.Items)
        {
            var item = items.FirstOrDefault(i => i.Id == lineItem.ItemId)
                ?? throw new NotFoundException($"Item {lineItem.ItemId} not found.");

            if (item.MinRankLevelToRequest > requestorRankLevel)
            {
                throw new ConflictException(
                    $"Item {item.ItemName} requires rank level {item.MinRankLevelToRequest}, " +
                    $"but your rank is {requestorRankLevel}.");
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

        // Create request as a Draft (Plan §3.6: [*] --> Draft). It is invisible to the approver
        // until SubmitAsync moves it to Pending. Before 2026-09-04 requests were born Pending,
        // so "Save as Draft" put them straight into the approver's queue (audit finding C1).
        var request = new Request
        {
            RequestorEmployeeNumber = requestorEmployeeNumber,
            ApproverEmployeeNumber = approverEmployeeNumber,
            Status = "Draft",
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
                    ToStatus = "Draft",
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

        // Draft --> Pending is the only legal submit transition (Plan §3.6). It used to be
        // Pending --> Pending, a no-op that only wrote a history row — see CreateAsync.
        if (request.Status != "Draft")
        {
            throw new ConflictException($"Cannot submit a request in {request.Status} status.");
        }

        // Spending eligibility (Plan §3.6 "Draft -> Pending ... Total <= role threshold", T3.4,
        // TC-05). Until now the limit was computed for the Dashboard tile and never enforced, so
        // an Engineer with a 500 allowance could submit 50 000 (audit finding C7).
        //
        // The comparison is against RemainingThisMonth, not the raw monthly allowance: the
        // threshold is a monthly budget, so what matters is what is left after this month's
        // other commitments. [ASK] #6 is answered as hard-block, the Plan's stated default —
        // a system that stores thresholds and then allows over-limit submissions has no reason
        // to store them.
        //
        // No double-count: EligibilityQueries only counts Pending-and-beyond, and this request
        // is still Draft at this point, so RemainingThisMonth excludes the very total being
        // checked.
        var eligibility = await eligibilityQueries.GetForEmployeeAsync(submitterEmployeeNumber);
        if (request.TotalEstimatedCost > eligibility.RemainingThisMonth)
        {
            var overBy = request.TotalEstimatedCost - eligibility.RemainingThisMonth;
            throw new BusinessRuleException(
                $"This request totals {request.TotalEstimatedCost:0.00}, which is {overBy:0.00} over your " +
                $"remaining budget of {eligibility.RemainingThisMonth:0.00} for this month " +
                $"(monthly limit {eligibility.MaxAmountPerMonth:0.00}, already committed " +
                $"{eligibility.MonthToDateSpend:0.00}). It resets on {eligibility.MonthResetsOn:yyyy-MM-dd}.");
        }

        request.Status = "Pending";
        request.StatusHistory.Add(new RequestStatusHistory
        {
            FromStatus = "Draft",
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

        // Every line gets exactly one decision, matched by RequestItemId — not by position.
        if (command.LineDecisions.Count != request.Items.Count)
        {
            throw new ConflictException("Line decision count does not match request items.");
        }

        var linesById = request.Items.ToDictionary(i => i.Id);
        var decidedIds = new HashSet<int>();

        // Persist each decision onto its line (audit finding C2: these used to be counted and
        // then discarded, so a PartiallyApproved request could not say which lines were granted).
        // ApprovedQuantity is the figure any later stock issue must use, never Quantity.
        foreach (var decision in command.LineDecisions)
        {
            if (!linesById.TryGetValue(decision.RequestItemId, out var line))
            {
                throw new ConflictException($"Line {decision.RequestItemId} does not belong to request {request.Id}.");
            }

            if (!decidedIds.Add(decision.RequestItemId))
            {
                throw new ConflictException($"Line {decision.RequestItemId} was decided more than once.");
            }

            var kind = decision.Decision.ToLowerInvariant();
            line.Decision = kind;
            line.ApprovedQuantity = kind switch
            {
                "approved" => line.Quantity,
                "rejected" => 0,
                "modified" => decision.ModifiedQuantity
                    ?? throw new ConflictException($"Line {line.Id} is marked modified but has no quantity."),
                _ => throw new ConflictException($"Unknown decision '{decision.Decision}' for line {line.Id}."),
            };
        }

        // Header status (Plan §3.6): everything granted as asked -> Approved; nothing granted ->
        // Rejected; anything else (a rejected line OR a reduced quantity) -> PartiallyApproved.
        // The old rule only looked at approved/rejected counts, so an all-"modified" request
        // came out PartiallyApproved with no rejected line to explain why — now it does so
        // deliberately, because a reduced quantity is by definition not a full approval.
        var lineCount = request.Items.Count;
        var approvedAsAskedCount = request.Items.Count(i => i.Decision == "approved");
        var rejectedCount = request.Items.Count(i => i.Decision == "rejected");

        var newStatus = approvedAsAskedCount == lineCount ? "Approved"
            : rejectedCount == lineCount ? "Rejected"
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
        // The validator existed but was never injected, so this path ran unvalidated (audit C6).
        await approveCancelValidator.ValidateAndThrowAsync(
            new ApproveCancellationCommand(requestId, rowVersion, approved, reason));

        // StatusHistory MUST be loaded: a refusal reverts to the status the request held before
        // CancellationPending, and that is read from history below. Without this Include the
        // list was empty and every refusal fell back to "Approved" — wrong for a
        // PartiallyApproved request (audit finding C6).
        var request = await db.Requests
            .Include(r => r.Items)
            .Include(r => r.StatusHistory)
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

        // On refusal, revert to exactly where the request was when cancellation was requested:
        // the FromStatus of the most recent transition INTO CancellationPending (Approved or
        // PartiallyApproved — Plan §3.6). Read from the audit row itself rather than inferred
        // from "the last status that wasn't X", which is fragile once more states exist.
        var newStatus = approved
            ? "Cancelled"
            : request.StatusHistory
                .Where(h => h.ToStatus == "CancellationPending" && h.FromStatus != null)
                .OrderByDescending(h => h.CreatedAtUtc)
                .ThenByDescending(h => h.Id)
                .Select(h => h.FromStatus)
                .FirstOrDefault()
              ?? throw new ConflictException(
                  $"Request {requestId} has no recorded transition into CancellationPending; cannot revert.");

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

    public async Task<bool> DeleteDraftAsync(int requestId, int requestorEmployeeNumber)
    {
        var request = await db.Requests
            .FirstOrDefaultAsync(r => r.Id == requestId)
            ?? throw new NotFoundException($"Request {requestId} not found.");

        // Ownership check
        if (request.RequestorEmployeeNumber != requestorEmployeeNumber)
        {
            throw new NotFoundException("Request not accessible.");
        }

        // Draft is the ONLY deletable state (Plan §3.6 "Draft --> [*] : Delete draft"; "Never
        // DELETE a request. Status transitions only."). This used to allow Pending, which —
        // with Pending also meaning "submitted" — let a requestor erase a request already in
        // the approver's queue, cascade-deleting its audit history (audit finding C4).
        if (request.Status != "Draft")
        {
            return false;
        }

        db.Requests.Remove(request);
        await db.SaveChangesAsync();

        return true;
    }
}
