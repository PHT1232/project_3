using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Application.Interfaces.Auth;
using Infrastructure.Identity;

namespace WebApi.Services;

public class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public int? EmployeeNumber =>
        int.TryParse(Principal?.FindFirstValue(JwtRegisteredClaimNames.Sub), out var value) ? value : null;

    public string? Role => Principal?.FindFirstValue(ClaimTypes.Role);

    public int? RankLevel =>
        int.TryParse(Principal?.FindFirstValue(JwtTokenService.RankLevelClaimType), out var value) ? value : null;
}
