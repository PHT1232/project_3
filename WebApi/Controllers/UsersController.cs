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
    ICurrentUserService currentUserService) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "RequireManager")]
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
    [Authorize(Policy = "RequireManager")]
    public async Task<ActionResult<UserDto>> CreateUser(CreateUserRequest request)
    {
        var created = await userManagementService.CreateUserAsync(request);
        return CreatedAtAction(nameof(GetUsers), null, created);
    }

    [HttpPut("{empNo:int}")]
    [Authorize(Policy = "RequireManager")]
    public async Task<ActionResult<UserDto>> UpdateUser(int empNo, UpdateUserRequest request)
    {
        var updated = await userManagementService.UpdateUserAsync(empNo, request);
        return Ok(updated);
    }

    [HttpPatch("{empNo:int}/status")]
    [Authorize(Policy = "RequireManager")]
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
