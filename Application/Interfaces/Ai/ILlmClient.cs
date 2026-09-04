using Application.Exceptions;

namespace Application.Interfaces.Ai;

/// <summary>
/// The only seam between the Application layer and an LLM provider (Plan §5.2, T5.5:
/// <c>Application/Interfaces/ILlmClient.cs</c>). Implemented in <c>Infrastructure/Ai</c>;
/// stubbed in tests. The Application layer never sees an SDK type.
/// </summary>
public interface ILlmClient
{
    /// <summary>False when no API key is configured — the caller goes straight to the fallback.</summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Ask the model for a request draft. <paramref name="systemPrompt"/> carries the
    /// authoritative catalogue; <paramref name="userText"/> is passed as the untrusted user
    /// message and is never concatenated into the system prompt (Plan §5.2 rule 3).
    /// Returns the raw JSON text the model produced — parsing/validation is the caller's job.
    /// </summary>
    /// <exception cref="LlmUnavailableException">Timeout, provider error, refusal or empty reply.</exception>
    Task<LlmCompletion> DraftRequestAsync(string systemPrompt, string userText, CancellationToken cancellationToken = default);
}

/// <summary>Raw provider reply plus the usage numbers the AiInteractionLog needs.</summary>
public sealed record LlmCompletion(string Text, string Model, long? InputTokens, long? OutputTokens);
