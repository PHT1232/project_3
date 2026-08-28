using Application.DTOs.Common;
using Application.DTOs.Inventory;
using Application.Interfaces.Inventory;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Queries;

public class InventoryQueries(DataContext db) : IInventoryQueries
{
    private sealed record Snapshot(int Id, string ItemName, int QuantityAvailable, int ReorderLevel, decimal UnitCost, Guid RowVersion);

    public async Task<PagedResult<InventoryRowDto>> GetPagedAsync(int page, int pageSize)
    {
        var query = db.StationeryItems.Where(i => i.IsActive);
        var totalCount = await query.CountAsync();

        var snapshots = await query
            .OrderBy(i => i.ItemName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(i => new Snapshot(i.Id, i.ItemName, i.QuantityAvailable, i.ReorderLevel, i.UnitCost, i.RowVersion))
            .ToListAsync();

        return new PagedResult<InventoryRowDto>(snapshots.Select(ToRowDto).ToList(), page, pageSize, totalCount);
    }

    public async Task<InventorySummaryDto> GetSummaryAsync()
    {
        var items = await db.StationeryItems
            .Where(i => i.IsActive)
            .Select(i => new { i.QuantityAvailable, i.ReorderLevel, i.UnitCost })
            .ToListAsync();

        return new InventorySummaryDto(
            TotalItems: items.Count,
            LowStockAlerts: items.Count(i => i.QuantityAvailable <= i.ReorderLevel),
            TotalValue: items.Sum(i => i.UnitCost * i.QuantityAvailable));
    }

    public async Task<IReadOnlyList<InventoryRowDto>> GetLowStockAsync()
    {
        var snapshots = await db.StationeryItems
            .Where(i => i.IsActive && i.QuantityAvailable <= i.ReorderLevel)
            .OrderBy(i => i.ItemName)
            .Select(i => new Snapshot(i.Id, i.ItemName, i.QuantityAvailable, i.ReorderLevel, i.UnitCost, i.RowVersion))
            .ToListAsync();

        return snapshots.Select(ToRowDto).ToList();
    }

    public async Task<InventoryRowDto?> GetRowAsync(int itemId)
    {
        var snapshot = await db.StationeryItems
            .Where(i => i.Id == itemId)
            .Select(i => new Snapshot(i.Id, i.ItemName, i.QuantityAvailable, i.ReorderLevel, i.UnitCost, i.RowVersion))
            .FirstOrDefaultAsync();

        return snapshot is null ? null : ToRowDto(snapshot);
    }

    private static InventoryRowDto ToRowDto(Snapshot s) => new(
        s.Id, s.ItemName, s.QuantityAvailable, s.ReorderLevel, s.UnitCost, DeriveStatus(s.QuantityAvailable, s.ReorderLevel), s.RowVersion);

    /// <summary>
    /// Not specified by the plan — see Application/Interfaces/Inventory/IInventoryQueries.cs
    /// doc comment for the rationale (simple threshold, not a consumption-rate model).
    /// </summary>
    private static string DeriveStatus(int quantityAvailable, int reorderLevel)
    {
        if (quantityAvailable <= reorderLevel) return "REORDER_NOW";
        if (quantityAvailable <= reorderLevel * 1.5) return "WATCH";
        return "OK";
    }
}
