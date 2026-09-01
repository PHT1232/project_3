namespace Application.DTOs.Reports;

/// <summary>
/// Report 4 ("By Team") — approved spend grouped by the requestor's line manager
/// (Users.SuperiorEmployeeNumber → the manager's name). Lets a Business Manager or the
/// Managing Director compare teams. Only offered when <see cref="Scope"/> is Group or Org
/// (a single Manager has just one team); rows are sorted by cost descending and
/// <see cref="TeamExpenditureRowDto.PercentOfTotal"/> sums to exactly 100.00.
///
/// Requests whose requestor has no manager are grouped under "(no manager)".
/// </summary>
public sealed record TeamExpenditureReportDto(
    string Scope,
    DateOnly FromDate,
    DateOnly ToDate,
    decimal TotalApprovedCost,
    IReadOnlyList<TeamExpenditureRowDto> Rows,
    ReportInsightDto Insight);

public sealed record TeamExpenditureRowDto(
    int? ManagerEmployeeNumber,
    string TeamName,
    int MemberCount,
    int RequestCount,
    decimal ApprovedCost,
    decimal PercentOfTotal);
