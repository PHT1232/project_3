using Application.DTOs.Common;
using Application.DTOs.Suppliers;

namespace Application.Interfaces.Suppliers;

public interface ISupplierQueries
{
    Task<PagedResult<SupplierDto>> GetPagedAsync(int page, int pageSize, bool includeInactive);

    Task<bool> HasActiveItemsAsync(int supplierId);
}
