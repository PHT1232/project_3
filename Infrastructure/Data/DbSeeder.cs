using Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace Infrastructure.Data;

/// <summary>
/// Seeds the 4 roles from Plan §1.3/line 433 (Engineer 1 / Manager 2 / Business Manager 3 / MD 4).
/// No demo users are seeded — user creation is Manager+'s job via POST /api/v1/users.
/// </summary>
public static class DbSeeder
{
    public static readonly (string Name, int RankLevel)[] Roles =
    [
        ("Engineer", 1),
        ("Manager", 2),
        ("Business Manager", 3),
        ("Managing Director", 4),
    ];

    public static async Task SeedRolesAsync(RoleManager<ApplicationRole> roleManager)
    {
        foreach (var (name, rankLevel) in Roles)
        {
            if (await roleManager.RoleExistsAsync(name))
            {
                continue;
            }

            await roleManager.CreateAsync(new ApplicationRole { Name = name, RankLevel = rankLevel });
        }
    }
}
