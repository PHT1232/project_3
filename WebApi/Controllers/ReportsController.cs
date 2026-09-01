using Application.DTOs.Reports;
using Application.Interfaces.Auth;
using Application.Interfaces.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

/// <summary>
/// Manager cost reports (Plan §4.2 / §5, page-map §9), scoped per user.
///
/// Only <c>[Authorize]</c> — not <c>RequireManager</c>. A plain requestor still gets their
/// own spend here (and the "My Requests" tab); the row-level scope in
/// <see cref="IReportQueries"/> is what limits what each role sees, and it never returns
/// data outside the caller's reporting line (CLAUDE.md principle #9). The client hides the
/// tabs a role has no data for; it is not the control.
///
/// All endpoints take an inclusive <c>fromDate</c>/<c>toDate</c>; both default when omitted
/// (last 90 days), matching the query-default convention on the other list endpoints.
/// </summary>
[ApiController]
[Route("api/v1/reports")]
[Authorize]
public class ReportsController(IReportQueries reportQueries, ICurrentUserService currentUserService)
    : ControllerBase
{
    private int Actor => currentUserService.EmployeeNumber
        ?? throw new InvalidOperationException("Authenticated request missing employee number claim.");

    /// <summary>Approved spend per item and each item's share of the scoped total.</summary>
    [HttpGet("cost-by-item")]
    public async Task<ActionResult<CostByItemReportDto>> GetCostByItem(
        [FromQuery] DateOnly? fromDate = null,
        [FromQuery] DateOnly? toDate = null)
    {
        var (from, to) = ResolveWindow(fromDate, toDate);
        var result = await reportQueries.GetCostByItemAsync(Actor, from, to);
        return Ok(result);
    }

    /// <summary>Approved spend per item plus its distinct-requestor and request counts.</summary>
    [HttpGet("item-headcount")]
    public async Task<ActionResult<ItemHeadcountReportDto>> GetItemHeadcount(
        [FromQuery] DateOnly? fromDate = null,
        [FromQuery] DateOnly? toDate = null)
    {
        var (from, to) = ResolveWindow(fromDate, toDate);
        var result = await reportQueries.GetItemHeadcountAsync(Actor, from, to);
        return Ok(result);
    }

    /// <summary>Approved spend per month, the running cumulative total, and the top consumed items.</summary>
    [HttpGet("cumulative-cost")]
    public async Task<ActionResult<CumulativeCostReportDto>> GetCumulativeCost(
        [FromQuery] DateOnly? fromDate = null,
        [FromQuery] DateOnly? toDate = null)
    {
        var (from, to) = ResolveWindow(fromDate, toDate);
        var result = await reportQueries.GetCumulativeCostAsync(Actor, from, to);
        return Ok(result);
    }

    /// <summary>
    /// Approved spend grouped by line manager. Meaningful for a Business Manager or the MD;
    /// a narrower scope still gets correctly-scoped rows and the client hides the tab.
    /// </summary>
    [HttpGet("by-team")]
    public async Task<ActionResult<TeamExpenditureReportDto>> GetByTeam(
        [FromQuery] DateOnly? fromDate = null,
        [FromQuery] DateOnly? toDate = null)
    {
        var (from, to) = ResolveWindow(fromDate, toDate);
        var result = await reportQueries.GetTeamExpenditureAsync(Actor, from, to);
        return Ok(result);
    }

    /// <summary>The caller's own approved activity only, regardless of role.</summary>
    [HttpGet("my-activity")]
    public async Task<ActionResult<MyActivityReportDto>> GetMyActivity(
        [FromQuery] DateOnly? fromDate = null,
        [FromQuery] DateOnly? toDate = null)
    {
        var (from, to) = ResolveWindow(fromDate, toDate);
        var result = await reportQueries.GetMyActivityAsync(Actor, from, to);
        return Ok(result);
    }

    private static (DateOnly From, DateOnly To) ResolveWindow(DateOnly? fromDate, DateOnly? toDate)
    {
        var to = toDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var from = fromDate ?? to.AddDays(-90);
        return (from, to);
    }
}
