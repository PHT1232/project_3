using Application.DTOs.Common;
using Application.DTOs.Support;
using Application.Interfaces.Auth;
using Application.Interfaces.Support;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

/// <summary>
/// In-app support inbox (Help page "message the team"). Any authenticated user can send a
/// message; Manager+ read and triage them. No email is sent — SMTP is on the Plan's [CUT]
/// list; this is the in-app alternative (Option B of the contact-the-team decision).
/// Thin: bind → service/query → result.
/// </summary>
[ApiController]
[Route("api/v1/support")]
[Authorize]
public class SupportController(
    ISupportMessageService supportMessageService,
    ISupportMessageQueries supportMessageQueries,
    ICurrentUserService currentUserService
) : ControllerBase
{
    /// <summary>Send a message to the team. Any authenticated user.</summary>
    [HttpPost("messages")]
    public async Task<ActionResult<SupportMessageDto>> Send([FromBody] CreateSupportMessageCommand command)
    {
        var actor = currentUserService.EmployeeNumber
            ?? throw new InvalidOperationException("Authenticated request missing employee number claim.");

        var created = await supportMessageService.CreateAsync(command, actor);
        return CreatedAtAction(nameof(GetOne), new { id = created.Id }, created);
    }

    /// <summary>Triage list, newest first. Manager+ only.</summary>
    [HttpGet("messages")]
    [Authorize(Policy = "RequireManager")]
    public async Task<ActionResult<PagedResult<SupportMessageDto>>> List(
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await supportMessageQueries.GetPagedAsync(status, Math.Max(1, page), Math.Clamp(pageSize, 1, 200));
        return Ok(result);
    }

    /// <summary>Count of unresolved messages, for the sidebar badge. Manager+ only.</summary>
    [HttpGet("messages/open-count")]
    [Authorize(Policy = "RequireManager")]
    public async Task<ActionResult<int>> OpenCount()
        => Ok(await supportMessageQueries.GetOpenCountAsync());

    /// <summary>One message. Manager+ only.</summary>
    [HttpGet("messages/{id:int}")]
    [Authorize(Policy = "RequireManager")]
    public async Task<ActionResult<SupportMessageDto>> GetOne(int id)
    {
        var match = await supportMessageQueries.GetByIdAsync(id);
        return match is null ? NotFound() : Ok(match);
    }

    /// <summary>Resolve or reopen a message. Manager+ only.</summary>
    [HttpPatch("messages/{id:int}/status")]
    [Authorize(Policy = "RequireManager")]
    public async Task<ActionResult<SupportMessageDto>> SetStatus(int id, [FromBody] SetSupportMessageStatusRequest request)
    {
        var actor = currentUserService.EmployeeNumber
            ?? throw new InvalidOperationException("Authenticated request missing employee number claim.");

        return Ok(await supportMessageService.SetResolvedAsync(id, request.Resolved, actor));
    }
}
