using Core.Entities;
using Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace WebApi.IntegrationTests;

public static class CatalogueTestData
{
    public static async Task<(Category Category, Supplier Supplier)> SeedCategoryAndSupplierAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        var category = new Category { Name = "Test Category", IsActive = true };
        var supplier = new Supplier { Name = "Test Supplier", LeadTimeDays = 5, IsActive = true };
        db.Categories.Add(category);
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync();

        return (category, supplier);
    }

    public static async Task<StationeryItem> SeedItemAsync(
        IServiceProvider services, int categoryId, int? supplierId, int minRankLevelToRequest, int quantityAvailable = 50, int reorderLevel = 10)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        var item = new StationeryItem
        {
            ItemName = $"Item {Guid.NewGuid():N}",
            CategoryId = categoryId,
            SupplierId = supplierId,
            UnitOfMeasure = "Each",
            UnitCost = 5.00m,
            QuantityAvailable = quantityAvailable,
            ReorderLevel = reorderLevel,
            MinRankLevelToRequest = minRankLevelToRequest,
            IsActive = true,
        };

        db.StationeryItems.Add(item);
        await db.SaveChangesAsync();
        return item;
    }
}
