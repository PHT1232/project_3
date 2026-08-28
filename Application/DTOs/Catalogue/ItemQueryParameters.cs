namespace Application.DTOs.Catalogue;

public sealed record ItemQueryParameters(
    int Page,
    int PageSize,
    int? CategoryId,
    string? SearchTerm,
    bool IncludeInactive);
