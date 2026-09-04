namespace Application.DTOs.Requests;

/// <summary>
/// Input: requestor submits their Draft request for approval.
/// Transitions Draft → Pending and notifies both requestor and approver (Plan §3.6).
/// </summary>
public sealed record SubmitRequestCommand(int RequestId, Guid RowVersion);
