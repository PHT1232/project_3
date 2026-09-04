namespace Application.Interfaces.Requests;

using Application.DTOs.Common;
using Application.DTOs.Requests;

/// <summary>
/// Service for request lifecycle operations: create, approve/reject, withdraw, cancel.
/// All state transitions log status history rows, send notifications, and perform concurrency checks.
///
/// All methods are transactional — on error (validation, concurrency, not found),
/// the entire operation rolls back and no notification is sent.
///
/// Status flow (Plan §3.6):
/// Draft → Pending (requestor submits; only now does the approver see it)
/// Draft → deleted (the only deletable state)
/// Pending → Approved (all lines) / PartiallyApproved (some lines) / Rejected (no lines)
/// Pending → Withdrawn (requestor withdraws before approval)
/// Approved/PartiallyApproved → CancellationPending (requestor requests cancellation)
/// CancellationPending → Cancelled (approver approves cancellation) / back to Approved (denies)
/// Approved → Fulfilled (all lines fulfilled, M4+)
/// </summary>
public interface IRequestService
{
    /// <summary>
    /// Create a new stationery request. Caller is the requestor; their superior is resolved
    /// server-side as the approver.
    ///
    /// Returns the newly created request DTO in "Draft" status. Drafts are not visible to the
    /// approver; call SubmitAsync to send one for approval, or DeleteDraftAsync to discard it.
    /// Lines are validated: must exist, be active, and be within the requestor's rank eligibility.
    /// </summary>
    Task<RequestDto> CreateAsync(CreateRequestCommand command, int requestorEmployeeNumber);

    /// <summary>
    /// Submit a request for approval. Transitions Draft → Pending, logs the event, and
    /// notifies the approver and requestor (Plan §3.5 event 2).
    ///
    /// Concurrency check: RowVersion must match the current row's version, or 409 Conflict is returned.
    /// </summary>
    Task<RequestDto> SubmitAsync(int requestId, Guid rowVersion, int submitterEmployeeNumber);

    /// <summary>
    /// Approver makes a decision: Approved, PartiallyApproved, or Rejected.
    /// Transitions Pending → outcome, writes each line's Decision + ApprovedQuantity onto
    /// RequestItems, logs the event, and notifies both parties.
    ///
    /// Stock does NOT move here yet (Plan §3.6 says it should on Approved — still open; see
    /// PROJECT_AUDIT.md C8). When it is implemented, issue RequestItem.ApprovedQuantity, not Quantity.
    ///
    /// Concurrency check: RowVersion must match, or 409 Conflict is returned.
    /// </summary>
    Task<RequestDto> ApproveAsync(ApproveRequestCommand command, int approverEmployeeNumber);

    /// <summary>
    /// Requestor withdraws their own request. Transitions Pending → Withdrawn.
    /// Logs the event and notifies the approver.
    ///
    /// Can only withdraw a Pending request that has not been approved/rejected yet.
    /// </summary>
    Task<RequestDto> WithdrawAsync(int requestId, Guid rowVersion, int requestorEmployeeNumber);

    /// <summary>
    /// Requestor requests cancellation after approval (M4+ feature for request-fulfillment).
    /// Transitions Approved/PartiallyApproved → CancellationPending, logs the event,
    /// and notifies the approver.
    ///
    /// Approver then either confirms cancellation (Cancelled) or denies it
    /// (back to Approved/PartiallyApproved). Stock reversal on Cancelled (M4).
    /// </summary>
    Task<RequestDto> RequestCancellationAsync(int requestId, Guid rowVersion, int requestorEmployeeNumber, string? reason);

    /// <summary>
    /// Approver approves or denies a cancellation request.
    /// If approved: Transitions CancellationPending → Cancelled.
    /// If denied: Transitions CancellationPending → back to Approved/PartiallyApproved.
    /// </summary>
    Task<RequestDto> ApproveCancellationAsync(int requestId, Guid rowVersion, int approverEmployeeNumber, bool approved, string? reason);

    /// <summary>
    /// Delete a request in Draft status — the only deletable state (Plan §3.6). No notification.
    /// Returns true if deleted, false if the request is in any other status.
    /// Throws NotFound if the request does not exist or is not the caller's.
    /// </summary>
    Task<bool> DeleteDraftAsync(int requestId, int requestorEmployeeNumber);
}
