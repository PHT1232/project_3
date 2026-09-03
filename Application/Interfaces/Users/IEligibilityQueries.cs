using Application.DTOs.Users;

namespace Application.Interfaces.Users;

/// <summary>
/// Read model for role-based spending eligibility (Plan §4.2 / §M1 T1.6 "eligibility engine").
/// Resolves the caller's role thresholds and their month-to-date committed spend. A read-only
/// named query (Plan §2.4) — implemented in Infrastructure over AspNetRoles + Requests.
/// </summary>
public interface IEligibilityQueries
{
    /// <summary>
    /// The employee's current eligibility. If the user has no assigned role, thresholds are
    /// reported as 0 (they can raise nothing) rather than throwing.
    /// </summary>
    Task<EligibilityDto> GetForEmployeeAsync(int employeeNumber);
}
