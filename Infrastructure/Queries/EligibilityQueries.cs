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

        var monthToDateSpend = await db.Requests.AsNoTracking()
            .Where(request =>
                request.RequestorEmployeeNumber == employeeNumber
                && request.CreatedAtUtc >= monthStart
                && request.CreatedAtUtc < nextMonth
                && CommittedStatuses.Contains(request.Status))
            .SumAsync(request => (decimal?)request.TotalEstimatedCost) ?? 0m;

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
