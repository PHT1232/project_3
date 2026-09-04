namespace Application.Interfaces.Ai;

/// <summary>
/// Behaviour knobs for the request assistant, bound from configuration in Program.cs.
/// <see cref="Enabled"/> is the Plan §7 M5 rollback switch (<c>Features:AiAssistant</c>):
/// off means every call takes the keyword fallback — the page keeps working, nothing crashes.
/// </summary>
public sealed class AiAssistantOptions
{
    public bool Enabled { get; set; } = true;

    /// <summary>How many catalogue rows go into the prompt (Plan §5.2 rule 6: "trimmed to ~40 items").</summary>
    public int MaxCatalogueItemsInPrompt { get; set; } = 40;

    /// <summary>Upper bound on the free text; also the truncation length stored in the log.</summary>
    public int MaxUserTextLength { get; set; } = 1000;
}
