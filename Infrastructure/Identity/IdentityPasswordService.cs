using Application.Interfaces.Auth;
using Microsoft.AspNetCore.Identity;

namespace Infrastructure.Identity;

public class IdentityPasswordService(UserManager<ApplicationUser> userManager) : IPasswordService
{
    public async Task<IReadOnlyList<string>> ChangePasswordAsync(
        int employeeNumber, string currentPassword, string newPassword)
    {
        var user = await userManager.FindByIdAsync(employeeNumber.ToString());
        if (user is null)
        {
            return ["Current password is incorrect."];
        }

        // UserManager.ChangePasswordAsync rotates the security stamp internally on success.
        var result = await userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        return result.Succeeded
            ? Array.Empty<string>()
            : result.Errors.Select(e => e.Description).ToArray();
    }
}
