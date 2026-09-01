namespace Application.DTOs.Reports;

/// <summary>
/// The "My Requests" report tab — always the caller's own approved activity only, whatever
/// their role. A manager who is also a requestor sees their personal spend here, separate
/// from their team/group reports.
/// </summary>
public sealed record MyActivityReportDto(
    DateOnly FromDate,
    DateOnly ToDate,
    decimal ApprovedCost,
    int RequestCount,
    int ItemCount,
    string? TopItemName,
    IReadOnlyList<MyActivityRowDto> Rows,
    IReadOnlyList<CumulativePointDto> Points,
    ReportInsightDto Insight);

public sealed record MyActivityRowDto(
    int ItemId,
    string ItemName,
    string? CategoryName,
    decimal ApprovedCost,
    int UnitsApproved);
