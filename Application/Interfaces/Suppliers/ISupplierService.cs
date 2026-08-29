using Application.DTOs.Common;
using Application.DTOs.Suppliers;

namespace Application.Interfaces.Suppliers;

public interface ISupplierService
{
    Task<PagedResult<SupplierDto>> GetSuppliersAsync(int page, int pageSize, bool includeInactive);

    Task<SupplierDto?> GetSupplierByIdAsync(int supplierId);

    Task<SupplierDto> CreateSupplierAsync(CreateSupplierRequest request);

    Task<SupplierDto> UpdateSupplierAsync(int supplierId, UpdateSupplierRequest request);

    /// <summary>409 if active items reference this supplier.</summary>
    Task DeactivateSupplierAsync(int supplierId);
}
