using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Application.Exceptions;
using Application.Interfaces.Ai;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Ai;

/// <summary>
/// <see cref="ILlmClient"/> over the Google Gemini REST API (<c>models/{model}:generateContent</c>)
/// — Plan T5.5: <c>Infrastructure/Ai/LlmClient.cs</c>. Plain <see cref="HttpClient"/> via
/// <see cref="IHttpClientFactory"/>, no SDK: one POST, one JSON body, one JSON reply, so every
/// line can be explained at the whiteboard (Plan §5.4).
///
/// The catalogue travels as <c>system_instruction</c>; the user's text is the single
/// <c>user</c> turn (Plan §5.2 rule 3). The reply is constrained to JSON with a response schema
/// so the model can only hand back item ids and quantities. Every failure — timeout, 429, 5xx,
/// safety block, empty reply — becomes an <see cref="LlmUnavailableException"/> with a short
/// reason for the log; the service decides what to do about it.
/// </summary>
public sealed class GeminiLlmClient(
    IHttpClientFactory httpClientFactory,
    GeminiOptions options,
    ILogger<GeminiLlmClient> logger) : ILlmClient
{
    public const string HttpClientName = "Gemini";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public bool IsConfigured => !string.IsNullOrWhiteSpace(options.ApiKey);

    public async Task<LlmCompletion> DraftRequestAsync(string systemPrompt, string userText, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            throw new LlmUnavailableException("not-configured");
        }

        var body = new GenerateContentRequest(
            SystemInstruction: new Content(null, [new Part(systemPrompt)]),
            Contents: [new Content("user", [new Part(userText)])],
            GenerationConfig: new GenerationConfig(
                ResponseMimeType: "application/json",
                ResponseSchema: DraftSchema,
                MaxOutputTokens: options.MaxOutputTokens,
                Temperature: 0.2,
                ThinkingConfig: string.IsNullOrWhiteSpace(options.ThinkingLevel)
                    ? null
                    : new ThinkingConfig(options.ThinkingLevel)));

        var attempts = Math.Max(0, options.MaxRetries) + 1;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await SendOnceAsync(body, cancellationToken);
            }
            catch (LlmUnavailableException ex) when (attempt < attempts && IsRetryable(ex.Reason))
            {
                logger.LogWarning("Gemini attempt {Attempt}/{Attempts} failed ({Reason}); retrying", attempt, attempts, ex.Reason);
            }
        }
    }

    private async Task<LlmCompletion> SendOnceAsync(GenerateContentRequest body, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(HttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"models/{options.Model}:generateContent")
        {
            Content = JsonContent.Create(body, options: Json),
        };
        // Header, not query string — keeps the key out of request logs and proxies' URL records.
        request.Headers.Add("x-goog-api-key", options.ApiKey);

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, cancellationToken);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Gemini call timed out after {Seconds}s", options.TimeoutSeconds);
            throw new LlmUnavailableException("timeout", ex);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Gemini unreachable");
            throw new LlmUnavailableException("network", ex);
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                logger.LogWarning("Gemini rate limit hit");
                throw new LlmUnavailableException("rate-limited");
            }

            if ((int)response.StatusCode >= 500)
            {
                logger.LogWarning("Gemini returned {Status}", (int)response.StatusCode);
                throw new LlmUnavailableException("provider-error");
            }

            if (!response.IsSuccessStatusCode)
            {
                // Bad key, unknown model, malformed request — our side, never the user's.
                // Logged loudly (with the body, which Google keeps free of secrets), degraded quietly.
                var detail = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogError("Gemini rejected the request ({Status}): {Detail}", (int)response.StatusCode, detail);
                throw new LlmUnavailableException("provider-4xx");
            }

            GenerateContentResponse? parsed;
            try
            {
                parsed = await response.Content.ReadFromJsonAsync<GenerateContentResponse>(Json, cancellationToken);
            }
            catch (JsonException ex)
            {
                throw new LlmUnavailableException("bad-envelope", ex);
            }

            if (parsed?.PromptFeedback?.BlockReason is { } blockReason)
            {
                logger.LogWarning("Gemini blocked the prompt ({Reason})", blockReason);
                throw new LlmUnavailableException("refusal");
            }

            var candidate = parsed?.Candidates?.FirstOrDefault();
            if (candidate is null)
            {
                throw new LlmUnavailableException("empty-reply");
            }

            if (candidate.FinishReason is not (null or "STOP"))
            {
                logger.LogWarning("Gemini stopped early ({Reason})", candidate.FinishReason);
                throw new LlmUnavailableException(candidate.FinishReason == "MAX_TOKENS" ? "truncated" : "refusal");
            }

            var text = string.Concat(candidate.Content?.Parts?.Select(p => p.Text) ?? []);
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new LlmUnavailableException("empty-reply");
            }

            return new LlmCompletion(
                text,
                parsed?.ModelVersion ?? options.Model,
                parsed?.UsageMetadata?.PromptTokenCount,
                parsed?.UsageMetadata?.CandidatesTokenCount);
        }
    }

    private static bool IsRetryable(string reason) =>
        reason is "timeout" or "network" or "provider-error" or "rate-limited";

    /// <summary>
    /// Mirrors <c>Application.DTOs.Ai.LlmDraftProposal</c> in Gemini's OpenAPI-subset schema
    /// dialect. The model can only return item ids and quantities — not names, not prices.
    /// </summary>
    private static readonly object DraftSchema = new
    {
        type = "OBJECT",
        properties = new
        {
            items = new
            {
                type = "ARRAY",
                items = new
                {
                    type = "OBJECT",
                    properties = new
                    {
                        itemId = new { type = "INTEGER" },
                        quantity = new { type = "INTEGER" },
                    },
                    required = new[] { "itemId", "quantity" },
                },
            },
            requiredByDate = new { type = "STRING", nullable = true },
            note = new { type = "STRING", nullable = true },
        },
        required = new[] { "items" },
    };

    // --- Wire shapes (only the fields we send/read) -------------------------------------------

    private sealed record GenerateContentRequest(
        [property: JsonPropertyName("system_instruction")] Content SystemInstruction,
        Content[] Contents,
        GenerationConfig GenerationConfig);

    private sealed record Content(string? Role, Part[]? Parts);

    private sealed record Part(string? Text);

    private sealed record GenerationConfig(
        string ResponseMimeType,
        object ResponseSchema,
        int MaxOutputTokens,
        double Temperature,
        ThinkingConfig? ThinkingConfig);

    private sealed record ThinkingConfig(string ThinkingLevel);

    private sealed record GenerateContentResponse(
        Candidate[]? Candidates,
        PromptFeedback? PromptFeedback,
        UsageMetadata? UsageMetadata,
        string? ModelVersion);

    private sealed record Candidate(Content? Content, string? FinishReason);

    private sealed record PromptFeedback(string? BlockReason);

    private sealed record UsageMetadata(long? PromptTokenCount, long? CandidatesTokenCount);
}
