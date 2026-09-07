namespace Application.DTOs.Requests;

/// <summary>
/// Input: approver responds to a withdrawal request.
///
/// When a requestor asks to withdraw a Pending request, the approver can either:
/// - Approved = true: transition WithdrawalPending → Withdrawn (final; no stock impact, because
///   a request that was never approved never issued any).
/// - Approved = false: refuse it, transition back to Pending so the request returns to the
///   approver's queue for a normal approve/reject decision.
///
/// Mirrors <see cref="ApproveCancellationCommand"/> — the two flows are deliberately the same
/// shape (team decision 2026-09-07 making withdrawal two-step; see RequestStateMachine).
/// </summary>
public sealed record ApproveWithdrawalCommand(
    int RequestId,
    Guid RowVersion,
    bool Approved,
    string? Reason
);
