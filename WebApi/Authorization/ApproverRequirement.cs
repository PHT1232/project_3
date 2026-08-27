using System.IdentityModel.Tokens.Jwt;
using Application.Interfaces.Auth;
using Microsoft.AspNetCore.Authorization;

namespace WebApi.Authorization;

/// <summary>
/// RequireApprover = authenticated user who currently has direct reports (live check —
/// see docs/development/identity-and-user-management-implementation-plan.md §6).
/// </summary>
public class ApproverRequirement : IAuthorizationRequirement;

public class ApproverHandler(IAccountStore accountStore) : AuthorizationHandler<ApproverRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ApproverRequirement requirement)
    {
        var sub = context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (sub is null || !int.TryParse(sub, out var employeeNumber))
        {
            return;
        }

        var account = await accountStore.GetByEmployeeNumberAsync(employeeNumber);
        if (account is { IsApprover: true })
        {
            context.Succeed(requirement);
        }
    }
}
