using Application.DTOs.Auth;
using Application.Interfaces.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController(IAuthService authService, ICurrentUserService currentUserService) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request)
    {
        var response = await authService.LoginAsync(request);
        if (response is null)
        {
            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Invalid credentials",
                detail: "Those sign-in details are incorrect.");
        }

        return Ok(response);
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<CurrentUserDto>> Me()
    {
        if (currentUserService.EmployeeNumber is not { } employeeNumber)
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid session");
        }

        var user = await authService.GetCurrentUserAsync(employeeNumber);
        if (user is null)
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid session");
        }

        return Ok(user);
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        if (currentUserService.EmployeeNumber is not { } employeeNumber)
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid session");
        }

        await authService.ChangePasswordAsync(employeeNumber, request);
        return NoContent();
    }
}
