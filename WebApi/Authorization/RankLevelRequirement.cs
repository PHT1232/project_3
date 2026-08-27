using Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;

namespace WebApi.Authorization;

/// <summary>RequireManager = RankLevelRequirement(2) — Manager and above (Plan §3/§4.2).</summary>
public class RankLevelRequirement(int minimumRankLevel) : IAuthorizationRequirement
{
    public int MinimumRankLevel { get; } = minimumRankLevel;
}

public class RankLevelHandler : AuthorizationHandler<RankLevelRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        RankLevelRequirement requirement)
    {
        var claim = context.User.FindFirst(JwtTokenService.RankLevelClaimType)?.Value;
        if (int.TryParse(claim, out var rankLevel) && rankLevel >= requirement.MinimumRankLevel)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
