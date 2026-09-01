using Application.DTOs.Common;
using Application.DTOs.Requests;
using Application.Interfaces.Auth;
using Application.Interfaces.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

/// <summary>
/// Stationery request lifecycle endpoints (Plan §3.4–§4.2).
/// 
/// Accessible to any authenticated user. Ownership and role-based visibility are
/// enforced inside the service/query layer (CLAUDE.md principle #9).
/// </summary>
[ApiController]
[Route("api/v1/requests")]
[Authorize]
public class RequestsController(
    IRequestService requestService,
    IRequestQueries requestQueries,
    ICurrentUserService currentUserService
) : ControllerBase
{
    /// <summary>
    /// Get visible requests (caller's own + subordinate requests if approver, or all if Manager+).
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<RequestDto>>> GetVisible(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null)
    {
        var actor = currentUserService.EmployeeNumber
            ?? throw new InvalidOperationException("Authenticated request missing employee number claim.");

        var result = await requestQueries.GetVisibleAsync(page, pageSize, status, actor);
        return Ok(result);
    }

    /// <summary>
    /// Get the current user's own requests (Plan §4.2: GET /api/v1/requests/mine).
    /// </summary>
    [HttpGet("mine")]
    public async Task<ActionResult<PagedResult<RequestDto>>> GetMine(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null)
    {
        var actor = currentUserService.EmployeeNumber
            ?? throw new InvalidOperationException("Authenticated request missing employee number claim.");

        var result = await requestQueries.GetByRequestorAsync(actor, page, pageSize, actor, status);
        return Ok(result);
    }

    /// <summary>
    /// Get single request by ID with line items and status history.
    /// Returns 404 if request does not exist or is not visible to caller (CLAUDE.md #9).
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<RequestDto>> GetById(int id)
    {
        var actor = currentUserService.EmployeeNumber
            ?? throw new InvalidOperationException("Authenticated request missing employee number claim.");

        var request = await requestQueries.GetByIdAsync(id, actor);
        return request is null ? NotFound() : Ok(request);
    }

    /// <summary>
    /// Create a new stationery request in Pending status.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<RequestDto>> Create([FromBody] CreateRequestCommand command)
    {
        var actor = currentUserService.EmployeeNumber
            ?? throw new InvalidOperationException("Authenticated request missing employee number claim.");

        var result = await requestService.CreateAsync(command, actor);
        return CreatedAtAction(nameof(GetById), new { id = result.RequestId }, result);
    }

    /// <summary>
    /// Submit a pending request for approval.
    /// </summary>
    [HttpPost("{id:int}/submit")]
    public async Task<ActionResult<RequestDto>> Submit(int id, [FromBody] SubmitRequestCommand command)
    {
        if (command.RequestId != 0 && command.RequestId != id)
        {
            return BadRequest(new { error = "RequestId in URL does not match RequestId in command body." });
        }

        var actor = currentUserService.EmployeeNumber
            ?? throw new InvalidOperationException("Authenticated request missing employee number claim.");

        var result = await requestService.SubmitAsync(id, command.RowVersion, actor);
        return Ok(result);
    }

    /// <summary>
    /// Requestor withdraws their own request (Pending status only).
    /// </summary>
    [HttpPost("{id:int}/withdraw")]
    public async Task<ActionResult<RequestDto>> Withdraw(int id, [FromBody] WithdrawRequestCommand command)
    {
        if (command.RequestId != 0 && command.RequestId != id)
        {
            return BadRequest(new { error = "RequestId in URL does not match RequestId in command body." });
        }

        var actor = currentUserService.EmployeeNumber
            ?? throw new InvalidOperationException("Authenticated request missing employee number claim.");

        var result = await requestService.WithdrawAsync(id, command.RowVersion, actor);
        return Ok(result);
    }

    /// <summary>
    /// Requestor requests cancellation for an approved request (Approved/PartiallyApproved only).
    /// </summary>
    [HttpPost("{id:int}/request-cancellation")]
    public async Task<ActionResult<RequestDto>> RequestCancellation(int id, [FromBody] RequestCancellationCommand command)
    {
        if (command.RequestId != 0 && command.RequestId != id)
        {
            return BadRequest(new { error = "RequestId in URL does not match RequestId in command body." });
        }

        var actor = currentUserService.EmployeeNumber
            ?? throw new InvalidOperationException("Authenticated request missing employee number claim.");

        var result = await requestService.RequestCancellationAsync(id, command.RowVersion, actor, command.Reason);
        return Ok(result);
    }

    /// <summary>
    /// Delete a Pending request that has not been submitted.
    /// </summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeletePending(int id)
    {
        var actor = currentUserService.EmployeeNumber
            ?? throw new InvalidOperationException("Authenticated request missing employee number claim.");

        var deleted = await requestService.DeletePendingAsync(id, actor);
        return deleted ? NoContent() : BadRequest(new { error = "Only unsubmitted Pending requests can be deleted." });
    }

    /// <summary>
    /// Status counts for dashboard summary widget.
    /// </summary>
    [HttpGet("dashboard-summary")]
    public async Task<ActionResult<Dictionary<string, int>>> GetDashboardSummary()
    {
        var actor = currentUserService.EmployeeNumber
            ?? throw new InvalidOperationException("Authenticated request missing employee number claim.");

        var summary = await requestQueries.GetStatusSummaryForDashboardAsync(actor);
        return Ok(summary);
    }
}
