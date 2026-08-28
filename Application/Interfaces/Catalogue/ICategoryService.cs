using Application.DTOs.Catalogue;

namespace Application.Interfaces.Catalogue;

public interface ICategoryService
{
    Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync();

    Task<CategoryDto> CreateCategoryAsync(string name);

    Task<CategoryDto> UpdateCategoryAsync(int categoryId, string name);

    Task DeactivateCategoryAsync(int categoryId);
}
