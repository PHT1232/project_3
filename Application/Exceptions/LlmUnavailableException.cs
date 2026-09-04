namespace Application.Exceptions;

/// <summary>
/// Thrown by an <see cref="Interfaces.Ai.ILlmClient"/> when the provider cannot give a usable
/// answer — timeout, 5xx, rate limit, refusal, missing key. <c>RequestAssistantService</c>
/// catches it and switches to the deterministic fallback; it never reaches the middleware
/// (Plan §5.2 rule 4: "graceful degradation is mandatory").
/// </summary>
public class LlmUnavailableException(string reason, Exception? inner = null)
    : Exception($"LLM unavailable: {reason}", inner)
{
    /// <summary>Short machine-readable reason, persisted to AiInteractionLogs.FallbackReason.</summary>
    public string Reason { get; } = reason;
}
