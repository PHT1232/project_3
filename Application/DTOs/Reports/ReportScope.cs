namespace Application.DTOs.Reports;

/// <summary>
/// The resolved data scope for the calling user's cost reports. Derived server-side from
/// rank + whether they have direct reports — never accepted from the client. Report DTOs
/// expose it as a lowercase-free string (e.g. "Team"), matching how <c>InventoryRowDto.Status</c>
/// carries its state as a plain string rather than an enum + JSON converter.
/// </summary>
public enum ReportScope
{
    /// <summary>Only the caller's own requests. Users with no direct reports.</summary>
    Self,

    /// <summary>The caller plus their direct reports. Rank-2 approvers (Manager).</summary>
    Team,

    /// <summary>
    /// The caller, their direct-report managers, and those managers' teams — a fixed two
    /// levels, not a recursive walk. Rank-3 approvers (Business Manager).
    /// </summary>
    Group,

    /// <summary>Every request in the organisation. Rank 4 (Managing Director).</summary>
    Org,
}
