using Core.Entities;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

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

    // =====================================================================================
    // Demo data — dev only, gated behind Seed:DemoData=true (see WebApi/Program.cs).
    // Builds a reporting hierarchy under the bootstrap MD (#1) plus ~100 approved requests
    // so the role-scoped Reports page has something to show for every scope. Idempotent:
    // re-running is a no-op once employee #20 exists. Deterministic (fixed RNG seed).
    // =====================================================================================

    /// <summary>First employee number used by the demo hierarchy; its presence means "already seeded".</summary>
    public const int FirstDemoEmployeeNumber = 20;

    private const int DemoRngSeed = 20260830;

    public static async Task SeedDemoDataAsync(
        UserManager<ApplicationUser> userManager, DataContext db, string demoPassword)
    {
        if (await userManager.FindByIdAsync(FirstDemoEmployeeNumber.ToString()) is not null)
        {
            return;
        }

        var rng = new Random(DemoRngSeed);

        // --- 1. Hierarchy: MD #1 → 2 Business Managers → 4 Managers → 12 Engineers ----------
        // (empNo, name, roleName, rankLevel, superiorEmpNo)
        (int Emp, string Name, string Role, int Rank, int Superior)[] people =
        [
            (20, "Nora Vance", "Business Manager", 3, BootstrapAdminEmployeeNumber),
            (21, "Paul Reeves", "Business Manager", 3, BootstrapAdminEmployeeNumber),
            (22, "Amy Cole", "Manager", 2, 20),
            (23, "Ben Frost", "Manager", 2, 20),
            (24, "Cara Diaz", "Manager", 2, 21),
            (25, "Dan Webb", "Manager", 2, 21),
            (26, "Ella Reed", "Engineer", 1, 22),
            (27, "Finn Park", "Engineer", 1, 22),
            (28, "Gina Lowe", "Engineer", 1, 22),
            (29, "Hugo Bell", "Engineer", 1, 23),
            (30, "Ivy Shaw", "Engineer", 1, 23),
            (31, "Jack Mora", "Engineer", 1, 23),
            (32, "Kira Nash", "Engineer", 1, 24),
            (33, "Leo Ward", "Engineer", 1, 24),
            (34, "Mia Sung", "Engineer", 1, 24),
            (35, "Nate Cruz", "Engineer", 1, 25),
            (36, "Omar Fox", "Engineer", 1, 25),
            (37, "Pia Grant", "Engineer", 1, 25),
        ];

        foreach (var (emp, name, role, rank, superior) in people)
        {
            var user = new ApplicationUser
            {
                Id = emp,
                UserName = emp.ToString(),
                Email = $"demo{emp}@hmt.example",
                Name = name,
                SuperiorEmployeeNumber = superior,
                RankLevel = rank,
                IsActive = true,
            };

            var created = await userManager.CreateAsync(user, demoPassword);
            if (!created.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Failed to seed demo user {emp}: " + string.Join(", ", created.Errors.Select(e => e.Description)));
            }

            await userManager.AddToRoleAsync(user, role);
        }

        // --- 2. Catalogue (only if the DB has no meaningful catalogue yet) -----------------
        var itemIds = await db.StationeryItems.Where(i => i.IsActive).Select(i => new { i.Id, i.UnitCost }).ToListAsync();
        if (itemIds.Count < 8)
        {
            var categories = new[]
            {
                new Category { Name = "Paper & Notebooks" },
                new Category { Name = "Writing Instruments" },
                new Category { Name = "Tech & Accessories" },
                new Category { Name = "Organization" },
            };
            db.Categories.AddRange(categories);
            await db.SaveChangesAsync();

            (string Name, int Cat, decimal Cost)[] catalogue =
            [
                ("Standard A4 Copy Paper, 500 Sheets", 0, 6.40m),
                ("Spiral Bound Notebooks, A5, Ruled", 0, 3.20m),
                ("Sticky Notes, 76x76mm, Pack of 12", 0, 8.50m),
                ("Blue Ballpoint Pens, Box of 50", 1, 11.75m),
                ("Highlighters, Assorted Colors, Pack of 4", 1, 4.80m),
                ("Whiteboard Markers, Black, Box of 12", 1, 9.90m),
                ("USB Flash Drive 64GB", 2, 12.50m),
                ("Ergonomic Wireless Mouse", 2, 24.99m),
                ("Mechanical Keyboard", 2, 89.00m),
                ("Laser Printer Toner Cartridge", 2, 98.00m),
                ("USB-C Docking Station", 2, 145.50m),
                ("27-inch 4K Monitor", 2, 320.00m),
                ("Desktop Stapler, Standard", 3, 6.80m),
                ("Lever Arch Files, A4, Pack of 5", 3, 14.25m),
                ("Desk Organizer Tray, Mesh", 3, 18.00m),
            ];

            var newItems = catalogue.Select(c => new StationeryItem
            {
                ItemName = c.Name,
                CategoryId = categories[c.Cat].Id,
                UnitOfMeasure = "Each",
                UnitCost = c.Cost,
                QuantityAvailable = rng.Next(40, 500),
                ReorderLevel = rng.Next(10, 80),
                MinRankLevelToRequest = 1,
                IsActive = true,
            }).ToList();
            db.StationeryItems.AddRange(newItems);
            await db.SaveChangesAsync();

            itemIds = newItems.Select(i => new { i.Id, i.UnitCost }).ToList();
        }

        // --- 3. ~100 approved requests spread across the teams and the last ~150 days ------
        int[] requestors = [.. people.Select(p => p.Emp)];               // engineers + managers
        var superiorOf = people.ToDictionary(p => p.Emp, p => p.Superior);
        string[] statuses =
        [
            "Approved", "Approved", "Approved", "Approved", "Approved", "Approved",
            "Fulfilled", "Fulfilled", "PartiallyApproved", "Rejected",
        ];

        var now = DateTime.UtcNow;
        var requests = new List<Request>(100);

        for (var n = 0; n < 100; n++)
        {
            var requestor = requestors[rng.Next(requestors.Length)];
            var decidedAt = now.AddDays(-rng.Next(1, 150)).AddHours(-rng.Next(0, 24));
            var status = statuses[rng.Next(statuses.Length)];

            var lineCount = rng.Next(1, 5);
            var picked = new HashSet<int>();
            var items = new List<RequestItem>();
            var total = 0m;

            for (var l = 0; l < lineCount; l++)
            {
                var choice = itemIds[rng.Next(itemIds.Count)];
                if (!picked.Add(choice.Id))
                {
                    continue;
                }

                var qty = rng.Next(1, 16);
                var lineTotal = Math.Round(qty * choice.UnitCost, 2);
                total += lineTotal;
                items.Add(new RequestItem
                {
                    ItemId = choice.Id,
                    Quantity = qty,
                    UnitCostSnapshot = choice.UnitCost,
                    LineTotal = lineTotal,
                });
            }

            requests.Add(new Request
            {
                RequestorEmployeeNumber = requestor,
                ApproverEmployeeNumber = superiorOf[requestor],
                Status = status,
                TotalEstimatedCost = total,
                CreatedAtUtc = decidedAt.AddDays(-rng.Next(1, 6)),
                DecidedAtUtc = decidedAt,
                RequiredByDate = decidedAt.AddDays(rng.Next(3, 21)),
                Items = items,
            });
        }

        db.Requests.AddRange(requests);
        await db.SaveChangesAsync();
    }
}
