namespace Application.DTOs.Suppliers;

public sealed record CreateSupplierRequest(string Name, int LeadTimeDays);
