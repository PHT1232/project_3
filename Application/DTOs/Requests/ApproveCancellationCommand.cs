namespace Application.DTOs.Requests;

/// <summary>
/// Input: approver responds to a cancellation request (approval_transaction.drawio:
/// "Bắt tín hiệu 'Request Cancel Approve?'").
///
/// When a requestor initiates cancellation on an Approved/PartiallyApproved request,
/// the approver can either:
/// - Approved = true: transition CancellationPending → Cancelled (stock reversal M4+).
/// - Approved = false: deny cancellation, transition back to Approved/PartiallyApproved.
/// </summary>
public sealed record ApproveCancellationCommand(
    int RequestId,
    Guid RowVersion,
    bool Approved,
    string? Reason
);
