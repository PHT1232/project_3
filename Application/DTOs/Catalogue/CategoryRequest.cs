namespace Application.DTOs.Catalogue;

/// <summary>Wraps the bare string ICategoryService.CreateCategoryAsync/UpdateCategoryAsync take,
/// so the request body is a JSON object ({"name": "..."}) rather than a raw JSON string.</summary>
public sealed record CategoryRequest(string Name);
