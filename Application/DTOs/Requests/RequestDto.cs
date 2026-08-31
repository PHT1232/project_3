namespace Application.DTOs.Requests;

/// <summary>
/// Display model for a complete request — header + line items + status history.
/// Includes timestamp and cost information for rendering in list views and detail pages.
/// </summary>
public sealed record RequestDto(
    int RequestId,
    int RequestorEmployeeNumber,
    string? RequestorName,
    int? ApproverEmployeeNumber,
    string? ApproverName,
    string Status,
    decimal TotalEstimatedCost,
    DateTime? RequiredByDate,
    string? DecisionComment,
    DateTime CreatedAtUtc,
    DateTime? DecidedAtUtc,
    Guid RowVersion,
    IReadOnlyList<RequestItemDto> Items,
    IReadOnlyList<RequestStatusHistoryDto> StatusHistory
);

/// <summary>
/// Single line in a request. Snapshot of item name, supplier, and unit cost at the time
/// of request submission — subsequent catalogue price changes do not affect this history.
/// </summary>
public sealed record RequestItemDto(
    int RequestItemId,
    int ItemId,
    string ItemName,
    string? CategoryName,
    int? SupplierId,
    string? SupplierName,
    int Quantity,
    decimal UnitCostSnapshot,
    decimal LineTotal
);

/// <summary>
/// One status transition event in the request's life cycle. Tracks approvals, rejections,
/// withdrawals, etc. — the audit trail for Plan §3.5 (RequestStatusHistory events).
/// </summary>
public sealed record RequestStatusHistoryDto(
    int HistoryId,
    int RequestId,
    string? FromStatus,
    string ToStatus,
    int ActorEmployeeNumber,
    string? ActorName,
    string? Comment,
    DateTime CreatedAtUtc
);
