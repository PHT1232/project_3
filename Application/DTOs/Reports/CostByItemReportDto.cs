namespace Application.DTOs.Reports;

/// <summary>
/// Report 1 — approved spend per stationery item and each item's share of the scoped total,
/// over <see cref="FromDate"/>..<see cref="ToDate"/> inclusive. Rows are sorted by cost
/// descending; <see cref="CostByItemRowDto.PercentOfTotal"/> values sum to exactly 100.00
/// (the largest row absorbs the rounding residual — Plan §5 / TC-16).
///
/// Cost is SUM(RequestItems.LineTotal) over requests in an approved state
/// (Approved / PartiallyApproved) whose DecidedAtUtc falls in range, restricted
/// to the requestors in the caller's <see cref="Scope"/>.
/// </summary>
public sealed record CostByItemReportDto(
    string Scope,
    DateOnly FromDate,
    DateOnly ToDate,
    decimal TotalApprovedCost,
    IReadOnlyList<CostByItemRowDto> Rows,
    ReportInsightDto Insight);

public sealed record CostByItemRowDto(
    int ItemId,
    string ItemName,
    string? CategoryName,
    decimal ApprovedCost,
    decimal PercentOfTotal);
