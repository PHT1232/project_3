using System.Globalization;
using Application.DTOs.Reports;
using Application.Interfaces.Reports;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Queries;

/// <summary>
/// Cost-report aggregations with row-level scoping by the caller's place in the reporting
/// line (see <see cref="IReportQueries"/>). Scope is resolved from AspNetUsers/AspNetRoles
/// and applied as a <c>WHERE RequestorEmployeeNumber IN (...)</c> on the database query, so
/// out-of-scope spend never leaves the database — only the caller's own rows are fetched.
///
/// Aggregation itself (GROUP BY item/month/manager, DISTINCT counts) runs in memory over
/// that already-scoped, already-date-filtered row set, not as a further SQL GROUP BY.
/// <b>Deliberate deviation from the "SQL-side GROUP BY" default</b> (Plan §5, page-map §9):
/// composing GroupBy with the Request/StationeryItem/Category navigation joins this needs
/// does not translate on SQLite (the integration-test provider — Plan §10.2 requires it over
/// the EF InMemory provider for FK/transaction fidelity), only on SQL Server, which would
/// make the test suite lie about what ships. Given the bounded volume here (an eProject's
/// request history, not a multi-tenant SaaS table), materialising the scoped+dated rows and
/// aggregating with LINQ-to-Objects is correct and provider-portable — the same trade-off
/// <see cref="InventoryQueries"/>'s <c>GetSummaryAsync</c> already makes in this codebase.
/// </summary>
public class ReportQueries(DataContext db) : IReportQueries
{
    // CK_Requests_Status terminal states that represent committed spend. "Approved only" in
    // page-map §9; the status enum splits that into three (a partial approval and a fulfilled
    // order both committed money). Rejected / Withdrawn / Cancelled / Pending are excluded.
    private static readonly string[] ApprovedStatuses = ["Approved", "PartiallyApproved", "Fulfilled"];

    private const int TopConsumedCount = 5;

    // -------------------------------------------------------------------------------------
    // Scope resolution
    // -------------------------------------------------------------------------------------

    /// <returns>
    /// The caller's scope and the set of requestor employee numbers it covers. A null id set
    /// means "no restriction" (Org / Managing Director).
    /// </returns>
    private async Task<(ReportScope Scope, int[]? RequestorIds)> ResolveScopeAsync(int actor)
    {
        var exists = await db.Users.AsNoTracking().AnyAsync(u => u.Id == actor);
        if (!exists)
        {
            throw new ApplicationException($"User {actor} not found.");
        }

        // Rank comes from the assigned Identity role, not ApplicationUser.RankLevel — that
        // column is never populated by the real user-creation path (IdentityAccountAdapter /
        // IdentityUserStore both project rank from AspNetUserRoles -> AspNetRoles.RankLevel;
        // see CLAUDE.md K8). Mirror that here so scoping matches what /auth/me reports.
        var rankLevel = await (
            from ur in db.UserRoles
            join r in db.Roles on ur.RoleId equals r.Id
            where ur.UserId == actor
            select (int?)r.RankLevel
        ).FirstOrDefaultAsync() ?? 0;

        var hasDirectReports = await db.Users.AsNoTracking()
            .AnyAsync(u => u.SuperiorEmployeeNumber == actor && u.IsActive);

        if (rankLevel >= 4)
        {
            return (ReportScope.Org, null);
        }

        if (!hasDirectReports)
        {
            return (ReportScope.Self, [actor]);
        }

        var directReportIds = await db.Users.AsNoTracking()
            .Where(u => u.SuperiorEmployeeNumber == actor)
            .Select(u => u.Id)
            .ToListAsync();

        if (rankLevel <= 2)
        {
            // Manager: self + direct reports.
            return (ReportScope.Team, directReportIds.Append(actor).ToArray());
        }

        // Business Manager (rank 3): self + direct-report managers + those managers' teams.
        // Fixed two levels — deliberately not a recursive descendant walk.
        var secondLevelIds = await db.Users.AsNoTracking()
            .Where(u => u.SuperiorEmployeeNumber != null && directReportIds.Contains(u.SuperiorEmployeeNumber.Value))
            .Select(u => u.Id)
            .ToListAsync();

        return (ReportScope.Group, directReportIds.Concat(secondLevelIds).Append(actor).Distinct().ToArray());
    }

    // -------------------------------------------------------------------------------------
    // Scoped, dated, flat row set — the one query every report aggregates over in memory
    // -------------------------------------------------------------------------------------

    private static (DateTime FromUtc, DateTime ToExclusiveUtc) ToUtcWindow(DateOnly fromDate, DateOnly toDate) =>
        (fromDate.ToDateTime(TimeOnly.MinValue), toDate.AddDays(1).ToDateTime(TimeOnly.MinValue));

    private sealed record LineFlat(
        int ItemId, string ItemName, string? CategoryName, decimal LineTotal, int Quantity,
        int RequestorEmployeeNumber, int RequestId, int Year, int Month);

    private async Task<List<LineFlat>> ScopedFlatLinesAsync(int[]? requestorIds, DateTime fromUtc, DateTime toExclusiveUtc)
    {
        var query = db.RequestItems.AsNoTracking()
            .Where(ri =>
                ri.Request!.DecidedAtUtc != null
                && ri.Request.DecidedAtUtc >= fromUtc
                && ri.Request.DecidedAtUtc < toExclusiveUtc
                && ApprovedStatuses.Contains(ri.Request.Status));

        if (requestorIds != null)
        {
            query = query.Where(ri => requestorIds.Contains(ri.Request!.RequestorEmployeeNumber));
        }

        return await query
            .Select(ri => new LineFlat(
                ri.ItemId,
                ri.Item!.ItemName,
                ri.Item.Category != null ? ri.Item.Category.Name : null,
                ri.LineTotal,
                ri.Quantity,
                ri.Request!.RequestorEmployeeNumber,
                ri.RequestId,
                ri.Request.DecidedAtUtc!.Value.Year,
                ri.Request.DecidedAtUtc!.Value.Month))
            .ToListAsync();
    }

    private sealed record ItemTotal(int ItemId, string ItemName, string? CategoryName, decimal Cost, int Units);

    private static List<ItemTotal> ItemTotals(IEnumerable<LineFlat> lines) =>
        lines
            .GroupBy(x => new { x.ItemId, x.ItemName, x.CategoryName })
            .Select(g => new ItemTotal(g.Key.ItemId, g.Key.ItemName, g.Key.CategoryName, g.Sum(x => x.LineTotal), g.Sum(x => x.Quantity)))
            .OrderByDescending(x => x.Cost)
            .ToList();

    // -------------------------------------------------------------------------------------
    // Report 1 — cost by item
    // -------------------------------------------------------------------------------------

    public async Task<CostByItemReportDto> GetCostByItemAsync(int actor, DateOnly fromDate, DateOnly toDate)
    {
        var (scope, requestorIds) = await ResolveScopeAsync(actor);
        var (fromUtc, toExclUtc) = ToUtcWindow(fromDate, toDate);

        var items = ItemTotals(await ScopedFlatLinesAsync(requestorIds, fromUtc, toExclUtc));
        var total = items.Sum(x => x.Cost);
        var pct = PercentTo100(items.Select(x => x.Cost).ToList());

        var rows = items
            .Select((x, i) => new CostByItemRowDto(x.ItemId, x.ItemName, x.CategoryName, Money(x.Cost), pct[i]))
            .ToList();

        var insight = await PeriodDeltaInsightAsync(scope, requestorIds, fromDate, toDate, total);
        return new CostByItemReportDto(scope.ToString(), fromDate, toDate, Money(total), rows, insight);
    }

    // -------------------------------------------------------------------------------------
    // Report 2 — cost & headcount
    // -------------------------------------------------------------------------------------

    public async Task<ItemHeadcountReportDto> GetItemHeadcountAsync(int actor, DateOnly fromDate, DateOnly toDate)
    {
        var (scope, requestorIds) = await ResolveScopeAsync(actor);
        var (fromUtc, toExclUtc) = ToUtcWindow(fromDate, toDate);

        var lines = await ScopedFlatLinesAsync(requestorIds, fromUtc, toExclUtc);
        var items = ItemTotals(lines);

        var countByItem = lines
            .GroupBy(x => x.ItemId)
            .ToDictionary(
                g => g.Key,
                g => (RequestorCount: g.Select(x => x.RequestorEmployeeNumber).Distinct().Count(),
                      RequestCount: g.Select(x => x.RequestId).Distinct().Count()));

        var rows = items
            .Select(x => new ItemHeadcountRowDto(
                x.ItemId, x.ItemName, x.CategoryName, Money(x.Cost), x.Units,
                countByItem.TryGetValue(x.ItemId, out var c) ? c.RequestorCount : 0,
                countByItem.TryGetValue(x.ItemId, out var c2) ? c2.RequestCount : 0))
            .ToList();

        var total = items.Sum(x => x.Cost);
        var distinctRequestors = lines.Select(x => x.RequestorEmployeeNumber).Distinct().Count();

        var top = items.FirstOrDefault();
        var insight = top is null
            ? ReportInsightDto.Empty(ScopeLabel(scope))
            : new ReportInsightDto(
                "Composition", ScopeLabel(scope), Money(total), 0m, null,
                top.ItemName,
                total > 0m ? (double)Math.Round(top.Cost / total * 100m, 1) : 0d,
                distinctRequestors);

        return new ItemHeadcountReportDto(scope.ToString(), fromDate, toDate, Money(total), rows, insight);
    }

    // -------------------------------------------------------------------------------------
    // Report 3 — cumulative cost + top consumed
    // -------------------------------------------------------------------------------------

    public async Task<CumulativeCostReportDto> GetCumulativeCostAsync(int actor, DateOnly fromDate, DateOnly toDate)
    {
        var (scope, requestorIds) = await ResolveScopeAsync(actor);
        var (fromUtc, toExclUtc) = ToUtcWindow(fromDate, toDate);

        var lines = await ScopedFlatLinesAsync(requestorIds, fromUtc, toExclUtc);
        var points = BuildMonthlyPoints(lines);

        var items = ItemTotals(lines);
        var topConsumed = items
            .OrderByDescending(x => x.Units).ThenByDescending(x => x.Cost)
            .Take(TopConsumedCount)
            .Select(x => new TopConsumedItemDto(x.ItemId, x.ItemName, x.CategoryName, x.Units, Money(x.Cost)))
            .ToList();

        var total = points.Count == 0 ? 0m : points[^1].CumulativeCost;
        var insight = await PeriodDeltaInsightAsync(scope, requestorIds, fromDate, toDate, total);
        return new CumulativeCostReportDto(scope.ToString(), fromDate, toDate, Money(total), points, topConsumed, insight);
    }

    private static List<CumulativePointDto> BuildMonthlyPoints(IEnumerable<LineFlat> lines)
    {
        var monthly = lines
            .GroupBy(x => new { x.Year, x.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Cost = g.Sum(x => x.LineTotal) })
            .OrderBy(m => m.Year).ThenBy(m => m.Month)
            .ToList();

        var running = 0m;
        return monthly
            .Select(m =>
            {
                var periodCost = Money(m.Cost);
                running = Money(running + periodCost);
                var label = new DateTime(m.Year, m.Month, 1).ToString("MMM yyyy", CultureInfo.InvariantCulture);
                return new CumulativePointDto($"{m.Year:D4}-{m.Month:D2}", label, periodCost, running);
            })
            .ToList();
    }

    // -------------------------------------------------------------------------------------
    // Report 4 — by team
    // -------------------------------------------------------------------------------------

    public async Task<TeamExpenditureReportDto> GetTeamExpenditureAsync(int actor, DateOnly fromDate, DateOnly toDate)
    {
        var (scope, requestorIds) = await ResolveScopeAsync(actor);
        var (fromUtc, toExclUtc) = ToUtcWindow(fromDate, toDate);

        var lines = await ScopedFlatLinesAsync(requestorIds, fromUtc, toExclUtc);

        var requestorIdsInResult = lines.Select(x => x.RequestorEmployeeNumber).Distinct().ToList();
        var superiorOf = await db.Users.AsNoTracking()
            .Where(u => requestorIdsInResult.Contains(u.Id))
            .Select(u => new { u.Id, u.SuperiorEmployeeNumber })
            .ToDictionaryAsync(u => u.Id, u => u.SuperiorEmployeeNumber);

        var byManager = lines
            .GroupBy(x => superiorOf.GetValueOrDefault(x.RequestorEmployeeNumber))
            .Select(g => new
            {
                ManagerId = g.Key,
                Cost = g.Sum(x => x.LineTotal),
                RequestCount = g.Select(x => x.RequestId).Distinct().Count(),
                MemberCount = g.Select(x => x.RequestorEmployeeNumber).Distinct().Count(),
            })
            .ToList();

        var managerIds = byManager.Where(m => m.ManagerId != null).Select(m => m.ManagerId!.Value).ToList();
        var names = await db.Users.AsNoTracking()
            .Where(u => managerIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Name);

        var ordered = byManager.OrderByDescending(m => m.Cost).ToList();
        var pct = PercentTo100(ordered.Select(m => m.Cost).ToList());
        var rows = ordered
            .Select((m, i) => new TeamExpenditureRowDto(
                m.ManagerId,
                m.ManagerId != null && names.TryGetValue(m.ManagerId.Value, out var n) ? $"{n}'s team" : "(no manager)",
                m.MemberCount, m.RequestCount, Money(m.Cost), pct[i]))
            .ToList();

        var total = ordered.Sum(m => m.Cost);
        var insight = await PeriodDeltaInsightAsync(scope, requestorIds, fromDate, toDate, total);
        return new TeamExpenditureReportDto(scope.ToString(), fromDate, toDate, Money(total), rows, insight);
    }

    // -------------------------------------------------------------------------------------
    // "My Requests" tab — always self only
    // -------------------------------------------------------------------------------------

    public async Task<MyActivityReportDto> GetMyActivityAsync(int actor, DateOnly fromDate, DateOnly toDate)
    {
        int[] justMe = [actor];
        var (fromUtc, toExclUtc) = ToUtcWindow(fromDate, toDate);

        var lines = await ScopedFlatLinesAsync(justMe, fromUtc, toExclUtc);
        var items = ItemTotals(lines);
        var total = items.Sum(x => x.Cost);
        var requestCount = lines.Select(x => x.RequestId).Distinct().Count();
        var points = BuildMonthlyPoints(lines);

        var rows = items
            .Select(x => new MyActivityRowDto(x.ItemId, x.ItemName, x.CategoryName, Money(x.Cost), x.Units))
            .ToList();

        // Self scope, so the insight always speaks in the first person ("Your").
        var insight = await PeriodDeltaInsightAsync(ReportScope.Self, justMe, fromDate, toDate, total);
        return new MyActivityReportDto(
            fromDate, toDate, Money(total), requestCount, items.Count,
            items.FirstOrDefault()?.ItemName, rows, points, insight);
    }

    // -------------------------------------------------------------------------------------
    // Insight — this window vs the immediately-preceding equal-length window
    // -------------------------------------------------------------------------------------

    private async Task<ReportInsightDto> PeriodDeltaInsightAsync(
        ReportScope scope, int[]? requestorIds, DateOnly fromDate, DateOnly toDate, decimal currentTotal)
    {
        var label = ScopeLabel(scope);
        if (currentTotal <= 0m)
        {
            return ReportInsightDto.Empty(label);
        }

        var days = toDate.DayNumber - fromDate.DayNumber + 1;
        var prevTo = fromDate.AddDays(-1);
        var prevFrom = prevTo.AddDays(-(days - 1));
        var (prevFromUtc, prevToExclUtc) = ToUtcWindow(prevFrom, prevTo);
        var (curFromUtc, curToExclUtc) = ToUtcWindow(fromDate, toDate);

        var previousLines = await ScopedFlatLinesAsync(requestorIds, prevFromUtc, prevToExclUtc);
        var previousTotal = previousLines.Sum(x => x.LineTotal);

        double? changePercent = previousTotal <= 0m
            ? null
            : (double)Math.Round((currentTotal - previousTotal) / previousTotal * 100m, 1);

        // Top mover: the item whose spend rose the most between the two windows.
        var currentByItem = ItemTotals(await ScopedFlatLinesAsync(requestorIds, curFromUtc, curToExclUtc));
        var previousByItem = ItemTotals(previousLines).ToDictionary(x => x.ItemId, x => x.Cost);

        var driver = currentByItem
            .Select(x => new { x.ItemName, Delta = x.Cost - previousByItem.GetValueOrDefault(x.ItemId) })
            .OrderByDescending(x => x.Delta)
            .FirstOrDefault();

        return new ReportInsightDto(
            "PeriodDelta", label, Money(currentTotal), Money(previousTotal), changePercent,
            driver is { Delta: > 0m } ? driver.ItemName : null, null, null);
    }

    // -------------------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------------------

    private static string ScopeLabel(ReportScope scope) => scope switch
    {
        ReportScope.Self => "Your",
        ReportScope.Team => "Your team's",
        ReportScope.Group => "Your group's",
        _ => "Org",
    };

    private static decimal Money(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Percentages that sum to exactly 100.00 — the largest slice absorbs the rounding
    /// residual (computed as 100 − Σ(others); page-map §9 / TC-16).
    /// </summary>
    private static List<decimal> PercentTo100(IReadOnlyList<decimal> amounts)
    {
        var total = amounts.Sum();
        if (total <= 0m)
        {
            return amounts.Select(_ => 0m).ToList();
        }

        var pct = amounts.Select(a => Math.Round(a / total * 100m, 2)).ToList();

        var largest = 0;
        for (var i = 1; i < amounts.Count; i++)
        {
            if (amounts[i] > amounts[largest])
            {
                largest = i;
            }
        }

        var others = pct.Where((_, i) => i != largest).Sum();
        pct[largest] = Math.Round(100m - others, 2);
        return pct;
    }
}
