
using Application.DTOs.Common;
using Application.DTOs.Requests;
using Application.Interfaces.Auth;
using Application.Interfaces.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

/// <summary>
/// Request approval workflow endpoints (Plan §3.6/§4.2).
///
/// Approver-only actions: view the decision queue, approve/reject, decide on cancellations.
/// Thin by rule (CLAUDE.md #2): no try/catch here — ExceptionHandlingMiddleware maps
/// ValidationException → 400, NotFoundException → 404, ConflictException → 409 as RFC 7807
/// ProblemDetails, the same shape every other controller returns. This class used to catch
/// everything itself and turned a validation failure into a 500 (found while fixing audit C6).
/// </summary>
[ApiController]
[Route("api/v1/approvals")]
[Authorize]
public class ApprovalsController(
    IRequestService requestService,
    IRequestQueries requestQueries,
    ICurrentUserService currentUserService
) : ControllerBase
{
    /// <summary>
    /// Get requests pending the current user's approval (Pending status, where current user is approver).
    /// Only approvers can call this; returns paginated list.
    /// </summary>
    [HttpGet("pending")]
    public async Task<ActionResult<PagedResult<RequestDto>>> GetPendingApprovals(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var approverEmployeeNumber = currentUserService.EmployeeNumber
            ?? throw new InvalidOperationException("Authenticated request missing employee number.");

        var result = await requestQueries.GetPendingApprovalsAsync(page, pageSize, approverEmployeeNumber);
        return Ok(result);
    }

    /// <summary>
    /// Approve, reject, or partially approve a request.
    /// Approver's decision on line items and overall status.
    /// </summary>
    [HttpPost("{requestId:int}/approve")]
    public async Task<ActionResult<RequestDto>> ApproveRequest(
        int requestId,
        [FromBody] ApproveRequestCommand command)
    {
        if (requestId != command.RequestId)
        {
            return BadRequest(new { error = "RequestId in URL does not match RequestId in command body." });
        }

        var approverEmployeeNumber = currentUserService.EmployeeNumber
            ?? throw new InvalidOperationException("Authenticated request missing employee number.");

        var result = await requestService.ApproveAsync(command, approverEmployeeNumber);
        return Ok(result);
    }

    /// <summary>
    /// Approver responds to a cancellation request (approve or deny).
    /// Transitions CancellationPending → Cancelled (if approved) or back to Approved/PartiallyApproved (if denied).
    /// </summary>
    [HttpPost("{requestId:int}/cancel-approval")]
    public async Task<ActionResult<RequestDto>> ApproveCancellation(
        int requestId,
        [FromBody] ApproveCancellationCommand command)
    {
        if (requestId != command.RequestId)
        {
            return BadRequest(new { error = "RequestId in URL does not match RequestId in command body." });
        }

        var approverEmployeeNumber = currentUserService.EmployeeNumber
            ?? throw new InvalidOperationException("Authenticated request missing employee number.");

        var result = await requestService.ApproveCancellationAsync(
            command.RequestId,
            command.RowVersion,
            approverEmployeeNumber,
            command.Approved,
            command.Reason);
        return Ok(result);
    }
}
