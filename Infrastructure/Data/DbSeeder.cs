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

    /// <summary>
    /// Where an item's closing balance should land relative to its reorder level, so the seeded
    /// catalogue exercises all three bands that <c>InventoryQueries.DeriveStatus</c> returns
    /// (<c>qty &lt;= reorder</c> → REORDER_NOW, <c>qty &lt;= reorder * 1.5</c> → WATCH, else OK).
    /// Before this existed every seeded item finished comfortably above its reorder level, so the
    /// low-stock endpoint, the WATCH/REORDER_NOW badges and the dashboard's low-stock tile could
    /// never be demonstrated with seeded data.
    /// </summary>
    private enum StockPosture
    {
        Healthy,
        Watch,
        Reorder,
    }

    private sealed record ItemSeed(
        string Name,
        string UnitOfMeasure,
        decimal UnitCost,
        int ReorderLevel,
        int MinRank,
        StockPosture Posture);

    /// <summary>
    /// Real per-category products. Each category has its own item list — previously a single
    /// 8-item template was applied to all 5 categories and the category name was prepended to
    /// each item, which produced nonsense like "Printing Supplies — Ballpoint Pens" and listed
    /// the same 8 products five times over.
    /// </summary>
    private static readonly (string Category, ItemSeed[] Items)[] CatalogueSeeds =
    [
        ("Paper & Notebooks",
        [
            new("Standard A4 Copy Paper, 500 Sheets", "Ream", 6.40m, 100, 1, StockPosture.Healthy),
            new("A3 Copy Paper, 250 Sheets", "Ream", 9.80m, 40, 1, StockPosture.Healthy),
            new("Premium Cardstock, 100 Sheets", "Pack", 12.50m, 40, 2, StockPosture.Watch),
            new("Spiral Notebook, 200 Pages", "Each", 3.20m, 60, 1, StockPosture.Healthy),
            new("Legal Pad, Pack of 6", "Pack", 7.40m, 45, 1, StockPosture.Healthy),
            new("Sticky Notes, 3x3, 12-Pack", "Pack", 8.10m, 50, 1, StockPosture.Reorder),
            new("Sticky Flags, 5 Colours", "Pack", 4.30m, 40, 1, StockPosture.Healthy),
            new("Manila Envelopes, Box of 100", "Box", 11.20m, 30, 1, StockPosture.Healthy),
        ]),
        ("Writing Instruments",
        [
            new("Ballpoint Pens, Box of 12", "Box", 5.75m, 80, 1, StockPosture.Healthy),
            new("Gel Pens, Box of 12", "Box", 9.90m, 50, 1, StockPosture.Healthy),
            new("Mechanical Pencils, Box of 12", "Box", 7.25m, 60, 1, StockPosture.Watch),
            new("Highlighter Set, 6 Colours", "Pack", 6.60m, 45, 1, StockPosture.Reorder),
            new("Whiteboard Markers, Box of 8", "Box", 8.95m, 40, 1, StockPosture.Healthy),
            new("Permanent Markers, Box of 12", "Box", 10.40m, 35, 1, StockPosture.Healthy),
            new("Correction Tape, Pack of 4", "Pack", 5.20m, 40, 1, StockPosture.Healthy),
            new("Executive Fountain Pen", "Each", 42.00m, 10, 3, StockPosture.Watch),
        ]),
        ("Tech & Accessories",
        [
            new("Ergonomic Wireless Mouse", "Each", 24.99m, 15, 2, StockPosture.Healthy),
            new("Wireless Keyboard", "Each", 34.00m, 12, 2, StockPosture.Healthy),
            new("USB-C Hub, 6-Port", "Each", 39.50m, 10, 2, StockPosture.Reorder),
            new("USB Flash Drive, 64GB", "Each", 12.75m, 25, 1, StockPosture.Healthy),
            new("HDMI Cable, 2m", "Each", 9.40m, 20, 1, StockPosture.Healthy),
            new("Adjustable Laptop Stand", "Each", 45.00m, 8, 3, StockPosture.Watch),
            new("Noise-Cancelling Headset", "Each", 89.00m, 6, 3, StockPosture.Reorder),
            new("Webcam, 1080p", "Each", 55.00m, 8, 3, StockPosture.Healthy),
        ]),
        ("Organization",
        [
            new("Lever Arch File, A4", "Each", 4.60m, 60, 1, StockPosture.Healthy),
            new("Ring Binder, 2-Inch", "Each", 5.90m, 50, 1, StockPosture.Healthy),
            new("Document Wallet, Pack of 10", "Pack", 8.30m, 40, 1, StockPosture.Watch),
            new("Hanging File Folders, 25-Pack", "Pack", 14.20m, 25, 1, StockPosture.Healthy),
            new("Desk Organiser, 5-Compartment", "Each", 18.50m, 15, 2, StockPosture.Healthy),
            new("Storage Box, A4", "Each", 7.80m, 30, 1, StockPosture.Healthy),
            new("Paper Clips, Box of 200", "Box", 2.40m, 70, 1, StockPosture.Reorder),
            new("Binder Clips, Assorted, 60-Pack", "Pack", 6.10m, 45, 1, StockPosture.Healthy),
        ]),
        ("Printing Supplies",
        [
            new("Laser Toner Cartridge, Black", "Each", 78.00m, 12, 2, StockPosture.Watch),
            new("Laser Toner Cartridge, Cyan", "Each", 82.00m, 8, 2, StockPosture.Healthy),
            new("Inkjet Cartridge, Tri-Colour", "Each", 34.50m, 15, 2, StockPosture.Healthy),
            new("Printer Drum Unit", "Each", 120.00m, 5, 3, StockPosture.Reorder),
            new("Thermal Receipt Roll, 10-Pack", "Pack", 16.40m, 20, 1, StockPosture.Healthy),
            new("Label Sheets, A4, 100-Pack", "Pack", 13.60m, 25, 1, StockPosture.Healthy),
            new("Laminating Pouches, A4, 100-Pack", "Pack", 22.00m, 15, 1, StockPosture.Healthy),
            new("Whiteboard Cleaner Spray", "Each", 6.80m, 20, 1, StockPosture.Watch),
        ]),
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

        var categories = CatalogueSeeds
            .Select(seed => new Category { Name = seed.Category, IsActive = true })
            .ToList();
        db.Categories.AddRange(categories);

        var suppliers = SupplierSeeds
            .Select(s => new Supplier { Name = s.Name, LeadTimeDays = s.LeadTimeDays, IsActive = true })
            .ToList();
        db.Suppliers.AddRange(suppliers);

        await db.SaveChangesAsync();

        var items = new List<StationeryItem>();
        var postures = new List<StockPosture>();
        var random = new Random(20260828);

        for (var c = 0; c < CatalogueSeeds.Length; c++)
        {
            var category = categories[c];

            foreach (var seed in CatalogueSeeds[c].Items)
            {
                var supplier = suppliers[items.Count % suppliers.Count];

                items.Add(new StationeryItem
                {
                    ItemName = seed.Name,
                    CategoryId = category.Id,
                    SupplierId = supplier.Id,
                    UnitOfMeasure = seed.UnitOfMeasure,
                    UnitCost = seed.UnitCost,
                    ReorderLevel = seed.ReorderLevel,
                    MinRankLevelToRequest = seed.MinRank,
                    QuantityAvailable = 0,
                    IsActive = true,
                });
                postures.Add(seed.Posture);
            }
        }

        db.StationeryItems.AddRange(items);
        await db.SaveChangesAsync();

        for (var i = 0; i < items.Count; i++)
        {
            SeedTransactionHistory(db, items[i], postures[i], suppliers, random, seedActorEmployeeNumber);
        }

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Opening balance (Receipt, Reference="OPENING" — per m2 plan §12 open question #4),
    /// then ~90 days of synthetic Issue/Receipt/Adjustment activity. Tracks a running balance
    /// so StationeryItem.QuantityAvailable stays equal to SUM(ChangeQuantity), never negative.
    /// </summary>
    private static void SeedTransactionHistory(
        DataContext db,
        StationeryItem item,
        StockPosture posture,
        List<Supplier> suppliers,
        Random random,
        int actorEmployeeNumber)
    {
        var now = DateTime.UtcNow;

        // Items meant to finish low start nearer their reorder level and are issued more often,
        // so the ledger tells a plausible "this ran down over 90 days" story rather than being
        // levelled by one implausible bulk movement at the end.
        var openingQuantity = posture switch
        {
            StockPosture.Reorder => (int)(item.ReorderLevel * 1.5),
            StockPosture.Watch => item.ReorderLevel * 2,
            _ => item.ReorderLevel * 3,
        };
        var issueProbability = posture switch
        {
            StockPosture.Reorder => 0.88,
            StockPosture.Watch => 0.80,
            _ => 0.70,
        };
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

            if (roll < issueProbability)
            {
                txType = StockTransactionType.Issue;
                changeQuantity = -Math.Min(balance, random.Next(1, 6));
                if (changeQuantity == 0)
                {
                    continue;
                }
            }
            else if (roll < issueProbability + 0.2)
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

        // The random walk gets close to the intended posture but not exactly into the band, so
        // close with one ordinary-looking movement that lands it. Typed by sign (Issue for a
        // draw-down, Receipt for a top-up) and dated yesterday, so it reads as normal activity.
        var target = TargetBalanceFor(item.ReorderLevel, posture);
        var correction = target - balance;

        if (correction != 0)
        {
            var isIssue = correction < 0;

            db.StockTransactions.Add(new StockTransaction
            {
                ItemId = item.Id,
                TxType = isIssue ? StockTransactionType.Issue : StockTransactionType.Receipt,
                ChangeQuantity = correction,
                UnitCostSnapshot = item.UnitCost,
                Reference = isIssue ? null : "PO-" + random.Next(1000, 9999),
                SupplierId = isIssue ? null : item.SupplierId,
                CreatedAtUtc = now.AddDays(-1),
                CreatedByEmployeeNumber = actorEmployeeNumber,
            });

            balance = target;
        }

        item.QuantityAvailable = balance;
    }

    /// <summary>
    /// Closing balance for a posture, expressed against the item's own reorder level so the
    /// bands in <c>InventoryQueries.DeriveStatus</c> are hit regardless of the item's scale.
    /// Never returns less than 1 — a seeded item at zero stock would look like a data error.
    /// </summary>
    private static int TargetBalanceFor(int reorderLevel, StockPosture posture) => posture switch
    {
        // <= reorderLevel   → REORDER_NOW
        StockPosture.Reorder => Math.Max(1, (int)(reorderLevel * 0.6)),
        // > reorderLevel and <= reorderLevel * 1.5 → WATCH
        StockPosture.Watch => Math.Max(reorderLevel + 1, (int)(reorderLevel * 1.25)),
        // > reorderLevel * 1.5 → OK
        _ => (int)(reorderLevel * 2.5),
    };
}
