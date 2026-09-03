namespace Application.DTOs.Ai;

/// <summary>Row of <c>GET /api/v1/ai/usage-report</c> (Manager+).</summary>
public sealed record AiInteractionLogDto(
    long Id,
    int EmployeeNumber,
    string Feature,
    string Model,
    int LatencyMs,
    bool WasFallback,
    string? FallbackReason,
    long? InputTokens,
    long? OutputTokens,
    string UserText,
    int DraftItemCount,
    DateTime CreatedAtUtc);
