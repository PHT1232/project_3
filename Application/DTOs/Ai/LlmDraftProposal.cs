namespace Application.DTOs.Ai;

/// <summary>
/// The shape the model is asked to return (Plan §5.2 sequence diagram:
/// <c>{items:[{itemId,qty}], requiredByDate, note}</c>). This is an untrusted proposal —
/// every field is re-validated against the real catalogue before it becomes a
/// <see cref="DraftRequestDto"/>. Mutable properties because it is a System.Text.Json target.
/// </summary>
public sealed class LlmDraftProposal
{
    public List<LlmDraftItem> Items { get; set; } = [];

    /// <summary>ISO date (yyyy-MM-dd) or null. Kept as string so a malformed value fails softly.</summary>
    public string? RequiredByDate { get; set; }

    public string? Note { get; set; }
}

public sealed class LlmDraftItem
{
    public int ItemId { get; set; }

    public int Quantity { get; set; }
}
