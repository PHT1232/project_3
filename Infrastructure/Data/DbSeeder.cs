using Core.Entities;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

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

    private static readonly (string Name, int LeadTimeDays)[] SupplierSeeds =
    [
        ("OfficeMax Direct", 3),
        ("Global Paper Co.", 5),
        ("Metro Stationery Wholesale", 7),
        ("TechSupply Partners", 10),
        ("EcoWrite Manufacturing", 14),
        ("Budget Bulk Supplies", 21),
    ];

    private static readonly string[] CategorySeeds =
    [
        "Paper & Notebooks",
        "Writing Instruments",
        "Tech & Accessories",
        "Organization",
        "Printing Supplies",
    ];

    private static readonly (string Suffix, string UnitOfMeasure, decimal UnitCost, int ReorderLevel, int MinRank)[] ItemTemplate =
    [
        ("Standard A4 Copy Paper, 500 Sheets", "Ream", 6.40m, 100, 1),
        ("Premium Cardstock, 100 Sheets", "Pack", 12.50m, 40, 2),
        ("Spiral Notebook, 200 Pages", "Each", 3.20m, 60, 1),
        ("Sticky Notes, 3x3, 12-Pack", "Pack", 8.10m, 50, 1),
        ("Ballpoint Pens, Box of 12", "Box", 5.75m, 80, 1),
        ("Gel Pens, Box of 12", "Box", 9.90m, 50, 1),
        ("Mechanical Pencils, Box of 12", "Box", 7.25m, 60, 1),
        ("Highlighter Set, 6 Colors", "Pack", 6.60m, 45, 1),
    ];

    /// <summary>
    /// 5 categories, 6 suppliers, 40 items (8 per category, matching ItemTemplate), and ~90
    /// days of StockTransactions per item so M5's AI feature has consumption history. Idempotent
    /// — checks whether Categories already has rows before doing anything.
    /// </summary>
    public static async Task SeedCatalogueAndInventoryAsync(DataContext db, int seedActorEmployeeNumber)
    {
        if (await db.Categories.AnyAsync())
        {
            return;
        }

        var categories = CategorySeeds.Select(name => new Category { Name = name, IsActive = true }).ToList();
        db.Categories.AddRange(categories);

        var suppliers = SupplierSeeds
            .Select(s => new Supplier { Name = s.Name, LeadTimeDays = s.LeadTimeDays, IsActive = true })
            .ToList();
        db.Suppliers.AddRange(suppliers);

        await db.SaveChangesAsync();

        var items = new List<StationeryItem>();
        var random = new Random(20260828);

        foreach (var category in categories)
        {
            for (var i = 0; i < ItemTemplate.Length; i++)
            {
                var template = ItemTemplate[i];
                var supplier = suppliers[(items.Count) % suppliers.Count];

                items.Add(new StationeryItem
                {
                    ItemName = $"{category.Name} — {template.Suffix}",
                    CategoryId = category.Id,
                    SupplierId = supplier.Id,
                    UnitOfMeasure = template.UnitOfMeasure,
                    UnitCost = template.UnitCost,
                    ReorderLevel = template.ReorderLevel,
                    MinRankLevelToRequest = template.MinRank,
                    QuantityAvailable = 0,
                    IsActive = true,
                });
            }
        }

        db.StationeryItems.AddRange(items);
        await db.SaveChangesAsync();

        foreach (var item in items)
        {
            SeedTransactionHistory(db, item, suppliers, random, seedActorEmployeeNumber);
        }

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Opening balance (Receipt, Reference="OPENING" — per m2 plan §12 open question #4),
    /// then ~90 days of synthetic Issue/Receipt/Adjustment activity. Tracks a running balance
    /// so StationeryItem.QuantityAvailable stays equal to SUM(ChangeQuantity), never negative.
    /// </summary>
    private static void SeedTransactionHistory(
        DataContext db, StationeryItem item, List<Supplier> suppliers, Random random, int actorEmployeeNumber)
    {
        var now = DateTime.UtcNow;
        var openingQuantity = item.ReorderLevel * 3;
        var balance = openingQuantity;

        db.StockTransactions.Add(new StockTransaction
        {
            ItemId = item.Id,
            TxType = StockTransactionType.Receipt,
            ChangeQuantity = openingQuantity,
            UnitCostSnapshot = item.UnitCost,
            Reference = "OPENING",
            SupplierId = item.SupplierId,
            CreatedAtUtc = now.AddDays(-90),
            CreatedByEmployeeNumber = actorEmployeeNumber,
        });

        for (var daysAgo = 87; daysAgo >= 0; daysAgo -= random.Next(3, 6))
        {
            var roll = random.NextDouble();
            StockTransactionType txType;
            int changeQuantity;
            int? supplierId = null;
            string? reference = null;

            if (roll < 0.7)
            {
                txType = StockTransactionType.Issue;
                changeQuantity = -Math.Min(balance, random.Next(1, 6));
                if (changeQuantity == 0)
                {
                    continue;
                }
            }
            else if (roll < 0.9)
            {
                txType = StockTransactionType.Receipt;
                changeQuantity = random.Next(10, 30);
                supplierId = suppliers[random.Next(suppliers.Count)].Id;
                reference = "PO-" + random.Next(1000, 9999);
            }
            else
            {
                txType = StockTransactionType.Adjustment;
                changeQuantity = random.Next(0, 2) == 0 ? -Math.Min(balance, 2) : 2;
                if (changeQuantity == 0)
                {
                    continue;
                }
            }

            balance += changeQuantity;

            db.StockTransactions.Add(new StockTransaction
            {
                ItemId = item.Id,
                TxType = txType,
                ChangeQuantity = changeQuantity,
                UnitCostSnapshot = item.UnitCost,
                Reference = reference,
                SupplierId = supplierId,
                CreatedAtUtc = now.AddDays(-daysAgo),
                CreatedByEmployeeNumber = actorEmployeeNumber,
            });
        }

        item.QuantityAvailable = balance;
    }
}
