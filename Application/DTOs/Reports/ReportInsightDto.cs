namespace Application.DTOs.Reports;

/// <summary>
/// Pre-computed inputs for the one-line insight sentence shown at the top of a report.
/// The wording is built on the client (<c>lib/insights.js</c>); this only carries the
/// numbers, derived from the same scoped dataset the report itself uses plus the
/// immediately-preceding equal-length window — no extra data source.
/// </summary>
/// <param name="Kind">
/// "PeriodDelta" (spend this window vs the previous one), "Composition" (concentration of
/// spend, for the headcount report), or "Empty" (no data in range).
/// </param>
/// <param name="ScopeLabel">Possessive label for the sentence: "Your", "Your team's", "Your group's", "Org".</param>
/// <param name="CurrentTotal">Approved spend in the selected window.</param>
/// <param name="PreviousTotal">Approved spend in the immediately-preceding equal-length window.</param>
/// <param name="ChangePercent">Percent change current vs previous; null when the previous window had no spend.</param>
/// <param name="DriverLabel">The item/team that moved the total the most (largest absolute period-over-period change), or the largest item for a Composition insight.</param>
/// <param name="DriverSharePercent">For a Composition insight: the driver's share of the current total.</param>
/// <param name="DistinctRequestors">For a Composition insight: distinct requestors in the window.</param>
public sealed record ReportInsightDto(
    string Kind,
    string ScopeLabel,
    decimal CurrentTotal,
    decimal PreviousTotal,
    double? ChangePercent,
    string? DriverLabel,
    double? DriverSharePercent,
    int? DistinctRequestors)
{
    public static ReportInsightDto Empty(string scopeLabel) =>
        new("Empty", scopeLabel, 0m, 0m, null, null, null, null);
}
