using Application.DTOs.Catalogue;
using Application.DTOs.Common;
using Application.Interfaces.Auth;
using Application.Interfaces.Catalogue;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

/// <summary>Read-only, any authenticated role — role filtering happens inside IItemService.</summary>
[ApiController]
[Route("api/v1")]
[Authorize]
public class CatalogueController(
    ICategoryService categoryService,
    IItemService itemService,
    ICurrentUserService currentUserService) : ControllerBase
{
    [HttpGet("categories")]
    public async Task<ActionResult<IReadOnlyList<CategoryDto>>> GetCategories()
    {
        var categories = await categoryService.GetCategoriesAsync();
        return Ok(categories);
    }

    [HttpGet("items")]
    public async Task<ActionResult<PagedResult<ItemDto>>> GetItems(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] int? categoryId = null,
        [FromQuery] int? supplierId = null,
        [FromQuery] string? searchTerm = null,
        [FromQuery] bool includeInactive = false)
    {
        var parameters = new ItemQueryParameters(page, pageSize, categoryId, supplierId, searchTerm, includeInactive);
        var result = await itemService.GetItemsAsync(parameters, currentUserService.RankLevel ?? 0);
        return Ok(result);
    }

    [HttpGet("items/{id:int}")]
    public async Task<ActionResult<ItemDto>> GetItem(int id)
    {
        var item = await itemService.GetItemByIdAsync(id, currentUserService.RankLevel ?? 0);
        return item is null ? NotFound() : Ok(item);
    }
}
