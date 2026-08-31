namespace Application.DTOs.Requests;

/// <summary>
/// Input: requestor submits their Pending request for approval.
/// Transitions Pending → Pending (marked as "submitted" in status history) and notifies approver.
/// </summary>
public sealed record SubmitRequestCommand(int RequestId, Guid RowVersion);
