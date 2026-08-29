using Application.DTOs.Common;
using Application.DTOs.SupplierRequests;
using Application.Interfaces.Auth;
using Application.Interfaces.SupplierRequests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

/// <summary>
/// Supplier replenishment orders raised from the inventory cart. Manager+ only, matching every
/// other inventory route (Plan §4.2).
///
/// Creating an order does not move stock — that happens later through
/// POST /api/v1/inventory/{itemId}/receive when the goods actually arrive.
/// </summary>
[ApiController]
[Route("api/v1/supplier-requests")]
[Authorize(Policy = "RequireManager")]
public class SupplierRequestsController(
    ISupplierRequestService supplierRequestService,
    ISupplierRequestQueries supplierRequestQueries,
    ICurrentUserService currentUserService) : ControllerBase
{
    /// <summary>Submits the cart. Returns one created order per distinct supplier.</summary>
    [HttpPost]
    public async Task<ActionResult<IReadOnlyList<SupplierRequestDto>>> Create(
        CreateSupplierRequestCommand command)
    {
        var actor = currentUserService.EmployeeNumber
            ?? throw new InvalidOperationException("Authenticated request missing employee number claim.");

        var created = await supplierRequestService.CreateAsync(command, actor);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<SupplierRequestDto>>> GetAll(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        return Ok(await supplierRequestQueries.GetPagedAsync(page, pageSize));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<SupplierRequestDto>> GetById(int id)
    {
        var request = await supplierRequestQueries.GetByIdAsync(id);
        return request is null ? NotFound() : Ok(request);
    }
}
