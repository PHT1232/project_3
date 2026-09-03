namespace Core.Entities;

/// <summary>
/// One row per AI call (Plan §3 table 12, §5.2 rule 5 [RUBRIC]). This table is the evidence
/// behind the AI-Usage-Report: which feature ran, which model answered, how long it took and
/// whether the deterministic fallback fired instead of the LLM.
///
/// Append-only. Never updated, never deleted. The user's text is stored truncated so the
/// report can show what was asked; the model's raw reply is not stored — the validated draft
/// is what the user saw, and it never touches the database until they submit it themselves.
/// </summary>
public class AiInteractionLog
{
    public long Id { get; set; }

    /// <summary>
    /// FK to Infrastructure.Identity.ApplicationUser.Id (AspNetUsers). Core stays
    /// framework-independent, so no navigation property (same pattern as Notification).
    /// </summary>
    public int EmployeeNumber { get; set; }

    /// <summary>Which capability ran — "RequestAssistant" today (Plan §5.1 A1).</summary>
    public string Feature { get; set; } = string.Empty;

    /// <summary>Model id that answered, or "keyword-fallback" when the LLM was not used.</summary>
    public string Model { get; set; } = string.Empty;

    public int LatencyMs { get; set; }

    /// <summary>True when the keyword-matching fallback produced the draft (Plan §5.2 rule 4).</summary>
    public bool WasFallback { get; set; }

    /// <summary>Why the fallback fired (disabled / not-configured / timeout / bad-json / ...). Null when the LLM answered.</summary>
    public string? FallbackReason { get; set; }

    public long? InputTokens { get; set; }

    public long? OutputTokens { get; set; }

    /// <summary>The user's free text, truncated to 1000 characters.</summary>
    public string UserText { get; set; } = string.Empty;

    /// <summary>How many catalogue lines the validated draft contained.</summary>
    public int DraftItemCount { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
