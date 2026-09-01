using Application.DTOs.Reports;

namespace Application.Interfaces.Reports;

/// <summary>
/// Read-only cost-report aggregations over the stationery request history (Plan §4.2 / §5,
/// page-map §9). Every method resolves the caller's <see cref="ReportScope"/> from their rank
/// and reporting line and restricts the rows accordingly — a requestor only ever sees their
/// own spend, a manager only their direct team, a business manager only their group, the MD
/// the whole organisation. Out-of-scope rows are never returned (CLAUDE.md principle #9).
///
/// All figures are over requests in an approved state (Approved / PartiallyApproved /
/// Fulfilled) with DecidedAtUtc in [fromDate, toDate] inclusive, and use
/// RequestItems.LineTotal (the snapshot) — never the live catalogue price (CLAUDE.md #8).
///
/// The row-level scope filter runs in SQL (only the caller's rows are ever fetched); the
/// GROUP BY-shaped aggregation (by item / month / manager, DISTINCT counts) runs in memory
/// over that already-scoped, already-dated row set — see the implementation's doc comment
/// for why (SQLite/SQL-Server translation gap in the test suite, bounded data volume).
/// </summary>
public interface IReportQueries
{
    Task<CostByItemReportDto> GetCostByItemAsync(int actorEmployeeNumber, DateOnly fromDate, DateOnly toDate);

    Task<ItemHeadcountReportDto> GetItemHeadcountAsync(int actorEmployeeNumber, DateOnly fromDate, DateOnly toDate);

    Task<CumulativeCostReportDto> GetCumulativeCostAsync(int actorEmployeeNumber, DateOnly fromDate, DateOnly toDate);

    /// <summary>
    /// Spend grouped by line manager. Meaningful only for a Business Manager or the MD; for a
    /// narrower scope it still returns correctly-scoped rows (typically one), and the client
    /// hides the tab.
    /// </summary>
    Task<TeamExpenditureReportDto> GetTeamExpenditureAsync(int actorEmployeeNumber, DateOnly fromDate, DateOnly toDate);

    /// <summary>The caller's own activity only, regardless of role.</summary>
    Task<MyActivityReportDto> GetMyActivityAsync(int actorEmployeeNumber, DateOnly fromDate, DateOnly toDate);
}
