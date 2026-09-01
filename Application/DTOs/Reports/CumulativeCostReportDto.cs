namespace Application.DTOs.Reports;

/// <summary>
/// Report 3 — approved spend per calendar month and the running cumulative total over the
/// scoped window, plus the five items with the most units approved in that window.
/// <see cref="CumulativePointDto.CumulativeCost"/> is monotonically non-decreasing and the
/// last point equals <see cref="TotalApprovedCost"/>.
/// </summary>
public sealed record CumulativeCostReportDto(
    string Scope,
    DateOnly FromDate,
    DateOnly ToDate,
    decimal TotalApprovedCost,
    IReadOnlyList<CumulativePointDto> Points,
    IReadOnlyList<TopConsumedItemDto> TopConsumed,
    ReportInsightDto Insight);

public sealed record CumulativePointDto(
    string PeriodKey,
    string PeriodLabel,
    decimal PeriodCost,
    decimal CumulativeCost);

public sealed record TopConsumedItemDto(
    int ItemId,
    string ItemName,
    string? CategoryName,
    int UnitsApproved,
    decimal ApprovedCost);
