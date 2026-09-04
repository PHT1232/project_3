namespace Application.DTOs.Users;

/// <summary>
/// The caller's spending eligibility (Plan §4.2 <c>GET /users/me/eligibility</c>, <c>[SPEC]</c>):
/// their role's limits, what they have already committed this calendar month, and what is left.
///
/// Limits are per <b>role tier</b>; the spend is this employee's own (summed over their
/// requests). <see cref="MonthToDateSpend"/> counts requests <b>created</b> in the current
/// UTC month whose status is not Rejected / Withdrawn / Cancelled — i.e. money currently
/// spoken for, so a withdrawn request frees its amount back up.
/// </summary>
/// <param name="RemainingThisMonth"><c>max(0, MaxAmountPerMonth - MonthToDateSpend)</c>.</param>
/// <param name="MonthResetsOn">First day of next UTC month — when the monthly allowance refills.</param>
public sealed record EligibilityDto(
    string Role,
    int RankLevel,
    decimal MaxAmountPerRequest,
    decimal MaxAmountPerMonth,
    decimal MonthToDateSpend,
    decimal RemainingThisMonth,
    DateOnly MonthResetsOn);
