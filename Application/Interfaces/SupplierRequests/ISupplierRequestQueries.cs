using Application.DTOs.Common;
using Application.DTOs.SupplierRequests;

namespace Application.Interfaces.SupplierRequests;

/// <summary>
/// Named query interface — joins/aggregations never go through the generic IRepository
/// (CLAUDE.md architecture principle #4).
/// </summary>
public interface ISupplierRequestQueries
{
    Task<PagedResult<SupplierRequestDto>> GetPagedAsync(int page, int pageSize);

    Task<SupplierRequestDto?> GetByIdAsync(int supplierRequestId);
}
