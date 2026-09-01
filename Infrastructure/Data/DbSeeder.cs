using Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace Infrastructure.Data;

/// <summary>
/// Seeds the 4 roles from Plan §1.3/line 433 (Engineer 1 / Manager 2 / Business Manager 3 / MD 4)
/// and one bootstrap admin account. No catalogue/supplier/stock template data is seeded — that's
/// generated externally via scripts/generate_seed_sql.py against real employee numbers instead.
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

    /// <summary>Employee number of the one bootstrap account — see SeedBootstrapAdminAsync.</summary>
    public const int BootstrapAdminEmployeeNumber = 1;

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

    /// <summary>
    /// Not in the original M1 plan — discovered as a real gap while seeding M2's stock ledger:
    /// M1 deliberately seeds zero users, but every StockTransaction needs a valid
    /// CreatedByEmployeeNumber FK, and with no users at all there is also no way to sign in and
    /// create the first real user via the Manager+-only POST /api/v1/users. This seeds exactly
    /// one Managing Director account (top of hierarchy, no superior) so both problems are
    /// solved. The password is never hardcoded — it's passed in from configuration
    /// (Seed:BootstrapAdminPassword) so it isn't committed to source. Idempotent.
    /// </summary>
    public static async Task<int> SeedBootstrapAdminAsync(UserManager<ApplicationUser> userManager, string password)
    {
        var existing = await userManager.FindByIdAsync(BootstrapAdminEmployeeNumber.ToString());
        if (existing is not null)
        {
            return existing.Id;
        }

        var admin = new ApplicationUser
        {
            Id = BootstrapAdminEmployeeNumber,
            UserName = BootstrapAdminEmployeeNumber.ToString(),
            Email = "admin@hmt.local",
            Name = "System Admin",
            SuperiorEmployeeNumber = null,
            IsActive = true,
        };

        var result = await userManager.CreateAsync(admin, password);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                "Failed to seed bootstrap admin: " + string.Join(", ", result.Errors.Select(e => e.Description)));
        }

        await userManager.AddToRoleAsync(admin, "Managing Director");
        return admin.Id;
    }
}
