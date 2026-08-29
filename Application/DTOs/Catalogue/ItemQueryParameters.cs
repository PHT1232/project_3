namespace Application.DTOs.Catalogue;

public sealed record ItemQueryParameters(
    int Page,
    int PageSize,
    int? CategoryId,
    int? SupplierId,
    string? SearchTerm,
    bool IncludeInactive);
