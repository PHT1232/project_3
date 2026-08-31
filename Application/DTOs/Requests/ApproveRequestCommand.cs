namespace Application.DTOs.Requests;

/// <summary>
/// Input: approver makes a decision on a request (approve, reject, or partially approve).
///
/// To support partial approval, the client sends a decision for each line item:
/// 'approved', 'rejected' (the line is not fulfilled), or 'modified' (approver changed qty).
/// The service then computes the overall request status (if all approved → Approved, some approved
/// some rejected → PartiallyApproved, all rejected → Rejected).
///
/// Plan §3.6 / §4.2 specifies this flow; see docs/development/m3-or-m4-plan.md for full details.
/// </summary>
public sealed record ApproveRequestCommand(
    int RequestId,
    Guid RowVersion,
    IReadOnlyList<LineDecision> LineDecisions,
    string? Comment
);

/// <summary>
/// The approver's decision for each line item:
/// - 'approved': fulfilled as requested
/// - 'rejected': not fulfilled
/// - 'modified': approver reduced the quantity (see ModifiedQuantity)
/// </summary>
public sealed record LineDecision(
    int RequestItemId,
    string Decision,
    int? ModifiedQuantity
);
