using System.Globalization;
using System.Text.RegularExpressions;
using Application.DTOs.Ai;
using Application.DTOs.Catalogue;

namespace Application.Services.Ai;

/// <summary>
/// The deterministic fallback behind A1 (Plan §5.2 rule 4: "the demo must work with the
/// network unplugged"). Pure string matching over item names — no model, no network, no
/// randomness — so it is unit-testable and explainable on a whiteboard.
///
/// Also used to pick which catalogue rows go into the LLM prompt: items whose names overlap
/// the user's words are listed first, so trimming the prompt to ~40 rows rarely drops the
/// item the user actually asked for.
/// </summary>
public static partial class KeywordRequestMatcher
{
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "and", "for", "with", "need", "want", "please", "some", "box", "boxes", "pack", "packs",
        "of", "by", "before", "end", "next", "this", "week", "month", "get", "order", "request", "me",
        "can", "you", "would", "like", "also", "plus", "few", "couple",
    };

    /// <summary>Tokenises free text into lowercase alphabetic words ≥ 3 chars minus stop words, singularised.</summary>
    public static IReadOnlyList<string> Tokenise(string text) =>
        WordPattern().Matches(text)
            .Select(m => m.Value.ToLowerInvariant())
            .Where(w => w.Length >= 3 && !StopWords.Contains(w))
            .Select(Singularise)
            .Distinct()
            .ToList();

    /// <summary>Number of the item's own name words that appear in the user's text.</summary>
    public static int Score(ItemDto item, IReadOnlyList<string> userTokens)
    {
        var nameTokens = Tokenise(item.ItemName);
        return nameTokens.Count == 0 ? 0 : nameTokens.Count(userTokens.Contains);
    }

    /// <summary>Catalogue ordered by relevance to the text — matching items first, then alphabetical.</summary>
    public static IReadOnlyList<ItemDto> RankByRelevance(IReadOnlyList<ItemDto> catalogue, string text)
    {
        var tokens = Tokenise(text);
        return catalogue
            .Select(item => (item, score: Score(item, tokens)))
            .OrderByDescending(x => x.score)
            .ThenBy(x => x.item.ItemName, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.item)
            .ToList();
    }

    /// <summary>
    /// Builds a proposal from text alone. An item is picked when every word of its name
    /// appears in the text ("black pen" needs both "black" and "pen"); if nothing matches that
    /// strictly, items sharing at least one distinctive word are taken instead, capped at 5.
    /// </summary>
    public static LlmDraftProposal Match(IReadOnlyList<ItemDto> catalogue, string text, DateTime todayUtc)
    {
        var tokens = Tokenise(text);
        var scored = catalogue
            .Select(item => (item, score: Score(item, tokens), total: Tokenise(item.ItemName).Count))
            .Where(x => x.score > 0)
            .ToList();

        var exact = scored.Where(x => x.score == x.total).ToList();
        var chosen = (exact.Count > 0 ? exact : scored)
            .OrderByDescending(x => x.score)
            .ThenBy(x => x.item.ItemName, StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .Select(x => new LlmDraftItem { ItemId = x.item.ItemId, Quantity = QuantityNear(text, x.item.ItemName) })
            .ToList();

        return new LlmDraftProposal
        {
            Items = chosen,
            RequiredByDate = DateNear(text, todayUtc)?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Note = chosen.Count == 0 ? null : "Matched by keyword — the AI assistant was unavailable.",
        };
    }

    /// <summary>"2 black pens" → 2. Looks for a number within three words before any word of the item name.</summary>
    private static int QuantityNear(string text, string itemName)
    {
        foreach (var word in Tokenise(itemName))
        {
            var pattern = new Regex(@"\b(\d{1,4})\b(?:\s+\w+){0,3}?\s+\w*" + Regex.Escape(word) + @"\w*", RegexOptions.IgnoreCase);
            var match = pattern.Match(text);
            if (match.Success && int.TryParse(match.Groups[1].Value, out var qty) && qty > 0)
            {
                return qty;
            }
        }

        return 1;
    }

    /// <summary>Only the phrases a demo is likely to use; anything else stays null and the user picks the date.</summary>
    private static DateTime? DateNear(string text, DateTime todayUtc)
    {
        var lower = text.ToLowerInvariant();
        if (lower.Contains("tomorrow")) return todayUtc.Date.AddDays(1);
        if (lower.Contains("next week")) return todayUtc.Date.AddDays(7);
        if (lower.Contains("end of the month") || lower.Contains("end of month"))
        {
            return new DateTime(todayUtc.Year, todayUtc.Month, DateTime.DaysInMonth(todayUtc.Year, todayUtc.Month), 0, 0, 0, DateTimeKind.Utc);
        }

        var iso = IsoDatePattern().Match(text);
        if (iso.Success
            && DateTime.TryParseExact(iso.Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed)
            && parsed.Date >= todayUtc.Date)
        {
            return parsed.Date;
        }

        return null;
    }

    private static string Singularise(string word) =>
        word.Length > 3 && word.EndsWith('s') && !word.EndsWith("ss") ? word[..^1] : word;

    [GeneratedRegex(@"[A-Za-z]+")]
    private static partial Regex WordPattern();

    [GeneratedRegex(@"\b\d{4}-\d{2}-\d{2}\b")]
    private static partial Regex IsoDatePattern();
}
