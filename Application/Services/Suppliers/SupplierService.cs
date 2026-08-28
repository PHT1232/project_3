using Application.DTOs.Common;
using Application.DTOs.Suppliers;
using Application.Exceptions;
using Application.Interfaces.Suppliers;
using Core.Entities;
using Core.Interfaces;
using FluentValidation;

namespace Application.Services.Suppliers;

public class SupplierService(
    IRepository<Supplier> supplierRepository,
    ISupplierQueries supplierQueries,
    IValidator<CreateSupplierRequest> createValidator,
    IValidator<UpdateSupplierRequest> updateValidator) : ISupplierService
{
    public Task<PagedResult<SupplierDto>> GetSuppliersAsync(int page, int pageSize, bool includeInactive) =>
        supplierQueries.GetPagedAsync(page, pageSize, includeInactive);

    public async Task<SupplierDto?> GetSupplierByIdAsync(int supplierId)
    {
        var supplier = await supplierRepository.GetByIdAsync(supplierId);
        return supplier is null ? null : ToDto(supplier);
    }

    public async Task<SupplierDto> CreateSupplierAsync(CreateSupplierRequest request)
    {
        await createValidator.ValidateAndThrowAsync(request);

        var supplier = new Supplier
        {
            Name = request.Name,
            LeadTimeDays = request.LeadTimeDays,
            IsActive = true,
        };

        var created = await supplierRepository.AddAsync(supplier);
        return ToDto(created);
    }

    public async Task<SupplierDto> UpdateSupplierAsync(int supplierId, UpdateSupplierRequest request)
    {
        await updateValidator.ValidateAndThrowAsync(request);

        var supplier = await supplierRepository.GetByIdAsync(supplierId)
            ?? throw new NotFoundException($"Supplier {supplierId} not found.");

        if (supplier.RowVersion != request.RowVersion)
        {
            throw new ConflictException("This supplier was modified by someone else. Reload and try again.");
        }

        supplier.Name = request.Name;
        supplier.LeadTimeDays = request.LeadTimeDays;
        supplier.RowVersion = Guid.NewGuid();

        await supplierRepository.UpdateAsync(supplier);
        return ToDto(supplier);
    }

    public async Task DeactivateSupplierAsync(int supplierId)
    {
        var supplier = await supplierRepository.GetByIdAsync(supplierId)
            ?? throw new NotFoundException($"Supplier {supplierId} not found.");

        if (await supplierQueries.HasActiveItemsAsync(supplierId))
        {
            throw new ConflictException(
                $"Supplier {supplierId} still supplies active items and cannot be deactivated.");
        }

        supplier.IsActive = false;
        supplier.RowVersion = Guid.NewGuid();
        await supplierRepository.UpdateAsync(supplier);
    }

    private static SupplierDto ToDto(Supplier supplier) =>
        new(supplier.Id, supplier.Name, supplier.LeadTimeDays, supplier.IsActive, supplier.RowVersion);
}
