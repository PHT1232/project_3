using Application.DTOs.Ai;

namespace Application.Interfaces.Ai;

/// <summary>
/// A1 — AI Request Assistant (Plan §5.2). Turns free text into a validated, editable draft.
/// Never creates a request; the user reviews and submits through the normal
/// <c>POST /api/v1/requests</c> path.
/// </summary>
public interface IRequestAssistantService
{
    Task<DraftRequestDto> DraftAsync(
        RequestAssistantCommand command,
        int employeeNumber,
        int callerRankLevel,
        CancellationToken cancellationToken = default);
}
