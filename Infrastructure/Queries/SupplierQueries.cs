using Application.DTOs.Common;
using Application.DTOs.Suppliers;
using Application.Interfaces.Suppliers;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Queries;

public class SupplierQueries(DataContext db) : ISupplierQueries
{
    public async Task<PagedResult<SupplierDto>> GetPagedAsync(int page, int pageSize, bool includeInactive)
    {
        var query = db.Suppliers.Where(s => includeInactive || s.IsActive);
        var totalCount = await query.CountAsync();

        var items = await query
            .OrderBy(s => s.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new SupplierDto(s.Id, s.Name, s.LeadTimeDays, s.IsActive, s.RowVersion))
            .ToListAsync();

        return new PagedResult<SupplierDto>(items, page, pageSize, totalCount);
    }

    public Task<bool> HasActiveItemsAsync(int supplierId) =>
        db.StationeryItems.AnyAsync(i => i.SupplierId == supplierId && i.IsActive);
}
