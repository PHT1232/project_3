namespace Application.DTOs.Reports;

/// <summary>
/// Report 2 — approved spend per item plus how many DISTINCT employees requested it
/// (<see cref="ItemHeadcountRowDto.RequestorCount"/>; two requests by one person count once —
/// Plan §5 / TC-17), total units approved, and distinct request count, over the scoped window.
/// </summary>
public sealed record ItemHeadcountReportDto(
    string Scope,
    DateOnly FromDate,
    DateOnly ToDate,
    decimal TotalApprovedCost,
    IReadOnlyList<ItemHeadcountRowDto> Rows,
    ReportInsightDto Insight);

public sealed record ItemHeadcountRowDto(
    int ItemId,
    string ItemName,
    string? CategoryName,
    decimal ApprovedCost,
    int UnitsApproved,
    int RequestorCount,
    int RequestCount);
