using Application.DTOs.Ai;
using Application.DTOs.Common;
using Application.Interfaces.Ai;
using Application.Interfaces.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace WebApi.Controllers;

/// <summary>
/// AI endpoints (Plan §4.2, §5). Thin: bind → service → result. The service owns grounding,
/// validation, fallback and logging; this class owns nothing but routing and policies.
/// </summary>
[ApiController]
[Route("api/v1/ai")]
[Authorize]
public class AiController(
    IRequestAssistantService requestAssistantService,
    IAiUsageQueries aiUsageQueries,
    ICurrentUserService currentUserService
) : ControllerBase
{
    public const string RateLimitPolicy = "AiAssistant";

    /// <summary>
    /// A1 — natural language → validated, editable draft. Never creates a request
    /// (Plan §5.2 rule 1). Rate-limited per user (Plan §5.2 rule 6: 20 calls/hour).
    /// </summary>
    [HttpPost("request-assistant")]
    [EnableRateLimiting(RateLimitPolicy)]
    public async Task<ActionResult<DraftRequestDto>> DraftRequest(
        [FromBody] RequestAssistantCommand command,
        CancellationToken cancellationToken)
    {
        var employeeNumber = currentUserService.EmployeeNumber
            ?? throw new InvalidOperationException("Authenticated request missing employee number claim.");
        var rankLevel = currentUserService.RankLevel
            ?? throw new InvalidOperationException("Authenticated request missing rank level claim.");

        var draft = await requestAssistantService.DraftAsync(command, employeeNumber, rankLevel, cancellationToken);
        return Ok(draft);
    }

    /// <summary>AI usage log, newest first (Plan T5.6 [RUBRIC]). Manager+ only.</summary>
    [HttpGet("usage-report")]
    [Authorize(Policy = "RequireManager")]
    public async Task<ActionResult<PagedResult<AiInteractionLogDto>>> UsageReport(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await aiUsageQueries.GetPagedAsync(Math.Max(1, page), Math.Clamp(pageSize, 1, 200));
        return Ok(result);
    }
}
