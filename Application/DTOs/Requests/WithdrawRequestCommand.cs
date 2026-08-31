namespace Application.DTOs.Requests;

/// <summary>
/// Input: requestor withdraws their own request before approval.
/// Transitions Pending → Withdrawn. No stock impact.
/// 
/// The requestor can only withdraw a request that has not been approved or rejected yet.
/// </summary>
public sealed record WithdrawRequestCommand(
    int RequestId,
    Guid RowVersion
);
