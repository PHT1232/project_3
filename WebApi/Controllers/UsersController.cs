using Application.DTOs.Common;
using Application.DTOs.Users;
using Application.Interfaces.Auth;
using Application.Interfaces.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[ApiController]
[Route("api/v1/users")]
[Authorize]
public class UsersController(
    IUserManagementService userManagementService,
    IEligibilityQueries eligibilityQueries,
    ICurrentUserService currentUserService) : ControllerBase
{
    /// <summary>
    /// The caller's own spending eligibility — role limits, month-to-date committed spend and
    /// what's left (Plan §4.2, <c>[SPEC]</c>). Any authenticated user; every role has a limit.
    /// </summary>
    [HttpGet("me/eligibility")]
    public async Task<ActionResult<EligibilityDto>> GetMyEligibility()
    {
        var actor = currentUserService.EmployeeNumber
            ?? throw new InvalidOperationException("Authenticated request missing employee number claim.");

        return Ok(await eligibilityQueries.GetForEmployeeAsync(actor));
    }

    [HttpGet]
    [Authorize(Policy = "RequireBusinessManager")]
    public async Task<ActionResult<PagedResult<UserDto>>> GetUsers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? role = null,
        [FromQuery] string? location = null)
    {
        var result = await userManagementService.GetUsersAsync(page, pageSize, role, location);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = "RequireBusinessManager")]
    public async Task<ActionResult<UserDto>> CreateUser(CreateUserRequest request)
    {
        var created = await userManagementService.CreateUserAsync(request);
        return CreatedAtAction(nameof(GetUsers), null, created);
    }

    [HttpPut("{empNo:int}")]
    [Authorize(Policy = "RequireBusinessManager")]
    public async Task<ActionResult<UserDto>> UpdateUser(int empNo, UpdateUserRequest request)
    {
        var updated = await userManagementService.UpdateUserAsync(empNo, request);
        return Ok(updated);
    }

    [HttpPatch("{empNo:int}/status")]
    [Authorize(Policy = "RequireBusinessManager")]
    public async Task<ActionResult<UserDto>> SetStatus(int empNo, UserStatusRequest request)
    {
        var updated = await userManagementService.SetStatusAsync(empNo, request.IsActive);
        return Ok(updated);
    }

    [HttpGet("{empNo:int}/subordinates")]
    public async Task<ActionResult<IReadOnlyList<UserDto>>> GetSubordinates(int empNo)
    {
        var isSelf = currentUserService.EmployeeNumber == empNo;
        var isManager = (currentUserService.RankLevel ?? 0) >= 2;
        if (!isSelf && !isManager)
        {
            return Forbid();
        }

        var subordinates = await userManagementService.GetSubordinatesAsync(empNo);
        return Ok(subordinates);
    }
}
