using Application.DTOs.Users;
using Application.Interfaces.Users;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Queries;

/// <summary>
/// Role-based spending eligibility (see <see cref="IEligibilityQueries"/>). Resolves the
/// caller's role via AspNetUserRoles → AspNetRoles (the same path <see cref="ReportQueries"/>
/// uses for rank), then sums their own requests for the current UTC calendar month.
/// </summary>
public class EligibilityQueries(DataContext db) : IEligibilityQueries
{
    // Statuses that represent money currently committed against the monthly allowance.
    // Rejected / Withdrawn / Cancelled release it; Pending onward hold it.
    private static readonly string[] CommittedStatuses =
        ["Pending", "Approved", "PartiallyApproved", "CancellationPending"];

    public async Task<EligibilityDto> GetForEmployeeAsync(int employeeNumber)
    {
        var role = await (
                from ur in db.UserRoles
                join r in db.Roles on ur.RoleId equals r.Id
                where ur.UserId == employeeNumber
                select new { r.Name, r.RankLevel, r.MaxAmountPerRequest, r.MaxAmountPerMonth })
            .FirstOrDefaultAsync();

        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var nextMonth = monthStart.AddMonths(1);

        // Summed line by line rather than from Request.TotalEstimatedCost, because that column is
        // the snapshot of what was *asked for*. On a partially approved request the approver may
        // have cut quantities, and charging the requestor for stock they never received locked
        // them out of budget they still had — the same over-count the cost reports carried.
        //
        // (ApprovedQuantity ?? Quantity) reads correctly in all three cases: a Pending request has
        // no decision yet, so the full request is rightly held against the allowance; a decided
        // one charges what was granted; and a request decided before ApprovedQuantity existed
        // (2026-09-04) falls back to the requested figure, which for those rows is what was
        // granted anyway.
        var monthToDateSpend = await db.RequestItems.AsNoTracking()
            .Where(line =>
                line.Request!.RequestorEmployeeNumber == employeeNumber
                && line.Request.CreatedAtUtc >= monthStart
                && line.Request.CreatedAtUtc < nextMonth
                && CommittedStatuses.Contains(line.Request.Status))
            .SumAsync(line => (decimal?)((line.ApprovedQuantity ?? line.Quantity) * line.UnitCostSnapshot)) ?? 0m;

        var maxPerMonth = role?.MaxAmountPerMonth ?? 0m;
        var remaining = Math.Max(0m, Money(maxPerMonth - monthToDateSpend));

        return new EligibilityDto(
            role?.Name ?? string.Empty,
            role?.RankLevel ?? 0,
            role?.MaxAmountPerRequest ?? 0m,
            maxPerMonth,
            Money(monthToDateSpend),
            remaining,
            DateOnly.FromDateTime(nextMonth));
    }

    private static decimal Money(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
