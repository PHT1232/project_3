namespace Application.DTOs.Requests;

/// <summary>
/// Input: requestor requests cancellation of an already-approved request.
/// Transitions Approved/PartiallyApproved → CancellationPending.
/// Requires approver to then accept or deny the cancellation.
/// 
/// This is a multi-step flow: requestor initiates, approver decides.
/// </summary>
public sealed record RequestCancellationCommand(
    int RequestId,
    Guid RowVersion,
    string? Reason
);
