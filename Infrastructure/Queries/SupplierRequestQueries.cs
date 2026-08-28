using Application.DTOs.Common;
using Application.DTOs.SupplierRequests;
using Application.Interfaces.SupplierRequests;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Queries;

public class SupplierRequestQueries(DataContext db) : ISupplierRequestQueries
{
    public async Task<PagedResult<SupplierRequestDto>> GetPagedAsync(int page, int pageSize)
    {
        var totalCount = await db.SupplierRequests.CountAsync();

        var ids = await db.SupplierRequests
            .OrderByDescending(r => r.CreatedAtUtc)
            .ThenByDescending(r => r.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => r.Id)
            .ToListAsync();

        var items = new List<SupplierRequestDto>(ids.Count);

        foreach (var id in ids)
        {
            var dto = await GetByIdAsync(id);
            if (dto is not null)
            {
                items.Add(dto);
            }
        }

        return new PagedResult<SupplierRequestDto>(items, page, pageSize, totalCount);
    }

    public async Task<SupplierRequestDto?> GetByIdAsync(int supplierRequestId)
    {
        // Materialise then map, matching ItemQueries/InventoryQueries — keeps the projection
        // provider-safe across SQL Server and the SQLite test provider.
        var request = await db.SupplierRequests
            .AsNoTracking()
            .Include(r => r.Supplier)
            .Include(r => r.Items)
            .ThenInclude(i => i.Item)
            .FirstOrDefaultAsync(r => r.Id == supplierRequestId);

        if (request is null)
        {
            return null;
        }

        var lines = request.Items
            .OrderBy(i => i.Id)
            .Select(i => new SupplierRequestLineDto(
                i.ItemId,
                i.Item?.ItemName ?? string.Empty,
                i.Quantity,
                i.UnitCostSnapshot,
                i.LineTotal))
            .ToList();

        return new SupplierRequestDto(
            request.Id,
            request.SupplierId,
            request.Supplier?.Name ?? string.Empty,
            request.TotalCost,
            request.CreatedAtUtc,
            request.CreatedByEmployeeNumber,
            lines);
    }
}
