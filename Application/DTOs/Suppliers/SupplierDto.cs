namespace Application.DTOs.Suppliers;

public sealed record SupplierDto(int SupplierId, string Name, int LeadTimeDays, bool IsActive, Guid RowVersion);
