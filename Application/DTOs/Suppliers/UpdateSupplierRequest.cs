namespace Application.DTOs.Suppliers;

public sealed record UpdateSupplierRequest(string Name, int LeadTimeDays, Guid RowVersion);
