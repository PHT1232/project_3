using Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace WebApi.IntegrationTests;

public static class TestUserFactory
{
    public static async Task<ApplicationUser> CreateUserAsync(
        IServiceProvider services,
        int employeeNumber,
        string name,
        string email,
        string role,
        string password,
        int? superiorEmployeeNumber = null,
        bool isActive = true)
    {
        using var scope = services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = new ApplicationUser
        {
            Id = employeeNumber,
            UserName = employeeNumber.ToString(),
            Email = email,
            Name = name,
            SuperiorEmployeeNumber = superiorEmployeeNumber,
            IsActive = isActive,
        };

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join(", ", result.Errors.Select(e => e.Description)));
        }

        var roleResult = await userManager.AddToRoleAsync(user, role);
        if (!roleResult.Succeeded)
        {
            throw new InvalidOperationException(string.Join(", ", roleResult.Errors.Select(e => e.Description)));
        }

        return user;
    }
}
