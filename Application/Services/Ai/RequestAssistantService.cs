using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Application.DTOs.Ai;
using Application.DTOs.Catalogue;
using Application.Exceptions;
using Application.Interfaces.Ai;
using Application.Interfaces.Catalogue;
using Core.Entities;
using Core.Interfaces;
using FluentValidation;

namespace Application.Services.Ai;

/// <summary>
/// A1 — AI Request Assistant (Plan §5.2). Orchestrates: load the caller's visible catalogue →
/// ask the LLM (or the keyword fallback) for a proposal → validate every field against the
/// real catalogue → log the interaction → return a draft.
///
/// Engineering rules from the Plan, in order:
///  1. The LLM never writes to the database — the only row this service writes is the log.
///  2. Item ids the model returns that aren't in the loaded catalogue are discarded.
///  3. Any provider failure (timeout, 5xx, bad JSON, refusal, no key, feature off) degrades to
///     <see cref="KeywordRequestMatcher"/> with <c>WasFallback = true</c> and an honest warning.
/// </summary>
public class RequestAssistantService(
    IItemQueries itemQueries,
    ILlmClient llmClient,
    IRepository<AiInteractionLog> logRepository,
    IValidator<RequestAssistantCommand> validator,
    AiAssistantOptions options) : IRequestAssistantService
{
    public const string FeatureName = "RequestAssistant";
    public const string FallbackModelName = "keyword-fallback";
    public const string FallbackWarning = "The AI assistant was unavailable, so items were matched by keyword. Please check the items and quantities before submitting.";

    private const int MaxQuantity = 9999;
    private const int CatalogueLoadLimit = 500;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<DraftRequestDto> DraftAsync(
        RequestAssistantCommand command,
        int employeeNumber,
        int callerRankLevel,
        CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);

        var text = command.Text.Trim();
        var todayUtc = DateTime.UtcNow;

        // Role-filtered exactly like the catalogue page: MinRankLevelToRequest <= caller's rank,
        // active only. The model literally cannot suggest an item the user isn't allowed to see.
        var page = await itemQueries.GetPagedAsync(
            new ItemQueryParameters(1, CatalogueLoadLimit, null, null, null, IncludeInactive: false),
            callerRankLevel);
        var catalogue = page.Items;
        var byId = catalogue.ToDictionary(i => i.ItemId);

        var stopwatch = Stopwatch.StartNew();
        var (proposal, model, inputTokens, outputTokens, fallbackReason) = await ProposeAsync(text, catalogue, todayUtc, cancellationToken);
        stopwatch.Stop();

        var warnings = new List<string>();
        if (fallbackReason is not null)
        {
            warnings.Add(FallbackWarning);
        }

        var items = ValidateItems(proposal.Items, byId, warnings);
        var requiredBy = ValidateDate(proposal.RequiredByDate, todayUtc, warnings);

        if (items.Count == 0)
        {
            warnings.Add("No catalogue items matched your description. Try naming the items as they appear in the catalogue.");
        }

        await logRepository.AddAsync(new AiInteractionLog
        {
            EmployeeNumber = employeeNumber,
            Feature = FeatureName,
            Model = model,
            LatencyMs = (int)Math.Min(stopwatch.ElapsedMilliseconds, int.MaxValue),
            WasFallback = fallbackReason is not null,
            FallbackReason = fallbackReason,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            UserText = text.Length <= options.MaxUserTextLength ? text : text[..options.MaxUserTextLength],
            DraftItemCount = items.Count,
            CreatedAtUtc = todayUtc,
        });

        return new DraftRequestDto(
            items,
            requiredBy,
            string.IsNullOrWhiteSpace(proposal.Note) ? null : proposal.Note.Trim(),
            items.Sum(i => i.UnitCost * i.Quantity),
            warnings,
            WasFallback: fallbackReason is not null,
            model);
    }

    /// <summary>LLM first; on any failure, the keyword matcher. Never throws for provider problems.</summary>
    private async Task<(LlmDraftProposal Proposal, string Model, long? InputTokens, long? OutputTokens, string? FallbackReason)> ProposeAsync(
        string text,
        IReadOnlyList<ItemDto> catalogue,
        DateTime todayUtc,
        CancellationToken cancellationToken)
    {
        string? reason = null;

        if (!options.Enabled)
        {
            reason = "disabled";
        }
        else if (!llmClient.IsConfigured)
        {
            reason = "not-configured";
        }
        else
        {
            try
            {
                var promptCatalogue = KeywordRequestMatcher.RankByRelevance(catalogue, text)
                    .Take(options.MaxCatalogueItemsInPrompt)
                    .ToList();
                var systemPrompt = RequestAssistantPrompt.Build(promptCatalogue, todayUtc);

                var completion = await llmClient.DraftRequestAsync(systemPrompt, text, cancellationToken);
                var parsed = JsonSerializer.Deserialize<LlmDraftProposal>(completion.Text, JsonOptions);
                if (parsed is null)
                {
                    reason = "empty-json";
                }
                else
                {
                    return (parsed, completion.Model, completion.InputTokens, completion.OutputTokens, null);
                }
            }
            catch (LlmUnavailableException ex)
            {
                reason = ex.Reason;
            }
            catch (JsonException)
            {
                reason = "bad-json";
            }
        }

        return (KeywordRequestMatcher.Match(catalogue, text, todayUtc), FallbackModelName, null, null, reason);
    }

    /// <summary>
    /// Every proposed line is checked against the loaded catalogue. Unknown ids are dropped
    /// silently (Plan §5.2 rule 3) — counted once in a warning so the user knows something was
    /// skipped, without echoing whatever id the model made up.
    /// </summary>
    private static List<DraftRequestItemDto> ValidateItems(
        IEnumerable<LlmDraftItem> proposed,
        IReadOnlyDictionary<int, ItemDto> byId,
        List<string> warnings)
    {
        var result = new List<DraftRequestItemDto>();
        var seen = new HashSet<int>();
        var skipped = 0;

        foreach (var line in proposed)
        {
            if (!byId.TryGetValue(line.ItemId, out var item))
            {
                skipped++;
                continue;
            }

            if (!seen.Add(item.ItemId))
            {
                continue; // duplicate line — first one wins
            }

            var quantity = line.Quantity;
            if (quantity < 1)
            {
                warnings.Add($"Quantity for {item.ItemName} was invalid and has been set to 1.");
                quantity = 1;
            }
            else if (quantity > MaxQuantity)
            {
                warnings.Add($"Quantity for {item.ItemName} was capped at {MaxQuantity}.");
                quantity = MaxQuantity;
            }

            if (quantity > item.QuantityAvailable)
            {
                warnings.Add($"Only {item.QuantityAvailable} of {item.ItemName} in stock; you asked for {quantity}.");
            }

            result.Add(new DraftRequestItemDto(
                item.ItemId,
                item.ItemName,
                item.CategoryName,
                item.SupplierName,
                item.UnitOfMeasure,
                item.UnitCost,
                quantity,
                item.QuantityAvailable));
        }

        if (skipped > 0)
        {
            warnings.Add(skipped == 1
                ? "One suggested item is not in your catalogue and was skipped."
                : $"{skipped} suggested items are not in your catalogue and were skipped.");
        }

        return result;
    }

    /// <summary>Same rule as CreateRequestCommandValidator: compared as a UTC date, today is still valid.</summary>
    private static DateTime? ValidateDate(string? value, DateTime todayUtc, List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
        {
            warnings.Add("The suggested required-by date could not be read; please pick one.");
            return null;
        }

        if (parsed.Date < todayUtc.Date)
        {
            warnings.Add("The suggested required-by date was in the past and has been cleared.");
            return null;
        }

        return DateTime.SpecifyKind(parsed.Date, DateTimeKind.Utc);
    }
}
