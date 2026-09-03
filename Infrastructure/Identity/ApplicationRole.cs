using Microsoft.AspNetCore.Identity;

namespace Infrastructure.Identity;

/// <summary>
/// RankLevel drives the hierarchy checks in Plan §3/§4.2 (Manager+ = RankLevel >= 2).
///
/// The spending thresholds are the Plan's "Amount-Employee-role threshold mapping table"
/// (§3.3 table row 3, <c>[SPEC]</c>). The schema of record models these as a separate
/// <c>RoleThresholds</c> table 1:1 with <c>Roles</c>; here they are columns on
/// <c>AspNetRoles</c> instead, consistent with the Identity fold that already put
/// <c>RankLevel</c> on this entity (see CLAUDE.md K8). A limit is per <b>role tier</b>;
/// consumption is per <b>employee</b> (summed over that person's own requests).
/// </summary>
public class ApplicationRole : IdentityRole<int>
{
    public int RankLevel { get; set; }

    /// <summary>Largest total a single request by this role may reach before it is blocked/flagged.</summary>
    public decimal MaxAmountPerRequest { get; set; }

    /// <summary>An employee's spending allowance for one calendar month, by role.</summary>
    public decimal MaxAmountPerMonth { get; set; }
}
