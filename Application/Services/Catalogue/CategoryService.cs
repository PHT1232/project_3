using Application.DTOs.Catalogue;
using Application.Exceptions;
using Application.Interfaces.Catalogue;
using Core.Entities;
using Core.Interfaces;
using FluentValidation;
using FluentValidation.Results;

namespace Application.Services.Catalogue;

public class CategoryService(IRepository<Category> categoryRepository) : ICategoryService
{
    public async Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync()
    {
        var categories = await categoryRepository.GetAllAsync();
        return categories.Select(ToDto).OrderBy(c => c.Name).ToList();
    }

    public async Task<CategoryDto> CreateCategoryAsync(string name)
    {
        ValidateName(name);

        var category = await categoryRepository.AddAsync(new Category { Name = name, IsActive = true });
        return ToDto(category);
    }

    public async Task<CategoryDto> UpdateCategoryAsync(int categoryId, string name)
    {
        ValidateName(name);

        var category = await categoryRepository.GetByIdAsync(categoryId)
            ?? throw new NotFoundException($"Category {categoryId} not found.");

        category.Name = name;
        await categoryRepository.UpdateAsync(category);
        return ToDto(category);
    }

    public async Task DeactivateCategoryAsync(int categoryId)
    {
        var category = await categoryRepository.GetByIdAsync(categoryId)
            ?? throw new NotFoundException($"Category {categoryId} not found.");

        category.IsActive = false;
        await categoryRepository.UpdateAsync(category);
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 100)
        {
            throw new ValidationException(
                [new ValidationFailure(nameof(name), "Name is required and must be at most 100 characters.")]);
        }
    }

    private static CategoryDto ToDto(Category category) => new(category.Id, category.Name, category.IsActive);
}
