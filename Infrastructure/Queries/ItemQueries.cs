using Application.DTOs.Catalogue;
using Application.DTOs.Common;
using Application.Interfaces.Catalogue;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Queries;

public class ItemQueries(DataContext db) : IItemQueries
{
    public async Task<PagedResult<ItemDto>> GetPagedAsync(ItemQueryParameters parameters, int callerRankLevel)
    {
        var includeInactive = parameters.IncludeInactive && callerRankLevel >= 2;

        var query = db.StationeryItems
            .Where(i => includeInactive || i.IsActive)
            .Where(i => i.MinRankLevelToRequest <= callerRankLevel);

        if (parameters.CategoryId is { } categoryId)
        {
            query = query.Where(i => i.CategoryId == categoryId);
        }

        if (parameters.SupplierId is { } supplierId)
        {
            query = query.Where(i => i.SupplierId == supplierId);
        }

        if (!string.IsNullOrWhiteSpace(parameters.SearchTerm))
        {
            var term = parameters.SearchTerm.Trim();
            query = query.Where(i => EF.Functions.Like(i.ItemName, $"%{term}%"));
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderBy(i => i.ItemName)
            .Skip((parameters.Page - 1) * parameters.PageSize)
            .Take(parameters.PageSize)
            .Select(i => new ItemDto(
                i.Id, i.ItemName, i.CategoryId, i.Category!.Name, i.UnitOfMeasure, i.UnitCost,
                i.QuantityAvailable, i.ReorderLevel, i.MinRankLevelToRequest, i.IsActive, i.SupplierId,
                i.Supplier == null ? null : i.Supplier.Name, i.RowVersion))
            .ToListAsync();

        return new PagedResult<ItemDto>(items, parameters.Page, parameters.PageSize, totalCount);
    }

    public Task<ItemDto?> GetByIdAsync(int itemId, int callerRankLevel) =>
        db.StationeryItems
            .Where(i => i.Id == itemId && i.IsActive && i.MinRankLevelToRequest <= callerRankLevel)
            .Select(i => new ItemDto(
                i.Id, i.ItemName, i.CategoryId, i.Category!.Name, i.UnitOfMeasure, i.UnitCost,
                i.QuantityAvailable, i.ReorderLevel, i.MinRankLevelToRequest, i.IsActive, i.SupplierId,
                i.Supplier == null ? null : i.Supplier.Name, i.RowVersion))
            .FirstOrDefaultAsync();

    public Task<ItemDto?> GetByIdUnfilteredAsync(int itemId) =>
        db.StationeryItems
            .Where(i => i.Id == itemId)
            .Select(i => new ItemDto(
                i.Id, i.ItemName, i.CategoryId, i.Category!.Name, i.UnitOfMeasure, i.UnitCost,
                i.QuantityAvailable, i.ReorderLevel, i.MinRankLevelToRequest, i.IsActive, i.SupplierId,
                i.Supplier == null ? null : i.Supplier.Name, i.RowVersion))
            .FirstOrDefaultAsync();

    public Task<bool> CategoryExistsAsync(int categoryId) =>
        db.Categories.AnyAsync(c => c.Id == categoryId && c.IsActive);
}
