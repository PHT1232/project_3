using Application.DTOs.Catalogue;
using Application.Interfaces.Catalogue;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[ApiController]
[Route("api/v1")]
[Authorize(Policy = "RequireBusinessManager")]
public class ManagerCatalogueController(IItemService itemService, ICategoryService categoryService) : ControllerBase
{
    [HttpPost("items")]
    public async Task<ActionResult<ItemDto>> CreateItem(CreateItemRequest request)
    {
        var created = await itemService.CreateItemAsync(request);
        return CreatedAtAction(nameof(CatalogueController.GetItem), "Catalogue", new { id = created.ItemId }, created);
    }

    [HttpPut("items/{id:int}")]
    public async Task<ActionResult<ItemDto>> UpdateItem(int id, UpdateItemRequest request)
    {
        var updated = await itemService.UpdateItemAsync(id, request);
        return Ok(updated);
    }

    [HttpPatch("items/{id:int}/deactivate")]
    public async Task<IActionResult> DeactivateItem(int id)
    {
        await itemService.DeactivateItemAsync(id);
        return NoContent();
    }

    [HttpPost("categories")]
    public async Task<ActionResult<CategoryDto>> CreateCategory(CategoryRequest request)
    {
        var created = await categoryService.CreateCategoryAsync(request.Name);
        return CreatedAtAction(nameof(CatalogueController.GetCategories), "Catalogue", null, created);
    }

    [HttpPut("categories/{id:int}")]
    public async Task<ActionResult<CategoryDto>> UpdateCategory(int id, CategoryRequest request)
    {
        var updated = await categoryService.UpdateCategoryAsync(id, request.Name);
        return Ok(updated);
    }

    [HttpPatch("categories/{id:int}/deactivate")]
    public async Task<IActionResult> DeactivateCategory(int id)
    {
        await categoryService.DeactivateCategoryAsync(id);
        return NoContent();
    }
}
