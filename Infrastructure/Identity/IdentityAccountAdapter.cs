using Application.Interfaces.Auth;
using Microsoft.AspNetCore.Identity;

namespace Infrastructure.Identity;

public class IdentityAccountAdapter(
    UserManager<ApplicationUser> userManager,
    RoleManager<ApplicationRole> roleManager,
    SignInManager<ApplicationUser> signInManager) : IAccountStore
{
    public async Task<AccountVerificationResult> VerifyCredentialsAsync(int employeeNumber, string password)
    {
        var user = await userManager.FindByIdAsync(employeeNumber.ToString());
        if (user is null || !user.IsActive)
        {
            return AccountVerificationResult.Failed;
        }

        var result = await signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            return AccountVerificationResult.Failed;
        }

        return AccountVerificationResult.Success(await ProjectAsync(user));
    }

    public async Task<AccountProjection?> GetByEmployeeNumberAsync(int employeeNumber)
    {
        var user = await userManager.FindByIdAsync(employeeNumber.ToString());
        return user is null ? null : await ProjectAsync(user);
    }

    private async Task<AccountProjection> ProjectAsync(ApplicationUser user)
    {
        var roleName = (await userManager.GetRolesAsync(user)).FirstOrDefault() ?? string.Empty;
        var role = roleName.Length > 0 ? await roleManager.FindByNameAsync(roleName) : null;
        var isApprover = userManager.Users.Any(u => u.SuperiorEmployeeNumber == user.Id);

        return new AccountProjection(
            user.Id,
            user.Name,
            user.Email!,
            roleName,
            role?.RankLevel ?? 0,
            user.SuperiorEmployeeNumber,
            isApprover,
            user.IsActive);
    }
}
