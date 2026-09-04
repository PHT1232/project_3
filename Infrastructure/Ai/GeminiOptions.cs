namespace Infrastructure.Ai;

/// <summary>
/// Provider settings, bound from the <c>Gemini</c> configuration section in Program.cs.
/// <see cref="ApiKey"/> is deliberately empty in every checked-in appsettings file — it comes
/// from the <c>Gemini__ApiKey</c> environment variable or <c>dotnet user-secrets</c>
/// (Plan §5.2 rule 2 [RUBRIC]: "a key in git history is a security finding").
/// </summary>
public sealed class GeminiOptions
{
    public const string SectionName = "Gemini";

    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Google Gemini model id. Measured 2026-09-03 with this exact request shape:
    /// <c>gemini-2.5-flash</c> → 404 "no longer available to new users";
    /// <c>gemini-3.6-flash</c> → 28–50 s (it "thinks" for 400–540 tokens first), 2.6–19 s with
    /// <see cref="ThinkingLevel"/> = low; <c>gemini-3.5-flash-lite</c> → 1.4–2.2 s every time.
    /// The Plan's 10-second budget (§5.2 rule 4) decides it: the lite model is the default.
    /// </summary>
    public string Model { get; set; } = "gemini-3.5-flash-lite";

    /// <summary>
    /// Gemini 3.x <c>thinkingConfig.thinkingLevel</c> ("low" / "medium" / "high"). A grounded
    /// extraction gains nothing from long reasoning and the latency budget is 10 s, so "low".
    /// Null/empty omits the field (needed for models that reject it).
    /// </summary>
    public string? ThinkingLevel { get; set; } = "low";

    /// <summary>Gemini REST base. Overridable so a test or proxy can point elsewhere.</summary>
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta/";

    /// <summary>Plan §5.2 rule 4: 10-second timeout.</summary>
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>Plan §5.2 rule 4: one retry, applied to timeouts, connection errors and 5xx/429.</summary>
    public int MaxRetries { get; set; } = 1;

    /// <summary>Plan §5.2 rule 6: max_tokens capped. A draft is a few hundred tokens of JSON.</summary>
    public int MaxOutputTokens { get; set; } = 1024;
}
