namespace Application.DTOs.Requests;

/// <summary>
/// Input: requestor asks to withdraw their own request before it has been decided.
/// Transitions Pending → WithdrawalPending. No stock impact — a request that was never
/// approved never issued any stock.
///
/// This does NOT withdraw the request on its own. The approver must confirm it
/// (<see cref="ApproveWithdrawalCommand"/>) before it becomes Withdrawn — team decision
/// 2026-09-07, overriding Plan §3.6's unilateral withdraw. Reason is optional and is shown to
/// the approver when they decide, exactly as with <see cref="RequestCancellationCommand"/>.
/// </summary>
public sealed record WithdrawRequestCommand(
    int RequestId,
    Guid RowVersion,
    string? Reason
);
