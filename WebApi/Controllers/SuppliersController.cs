using Application.DTOs.Common;
using Application.DTOs.Suppliers;
using Application.Interfaces.Suppliers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[ApiController]
[Route("api/v1/suppliers")]
[Authorize(Policy = "RequireManager")]
public class SuppliersController(ISupplierService supplierService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<SupplierDto>>> GetSuppliers(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] bool includeInactive = false)
    {
        var result = await supplierService.GetSuppliersAsync(page, pageSize, includeInactive);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<SupplierDto>> GetSupplier(int id)
    {
        var supplier = await supplierService.GetSupplierByIdAsync(id);
        return supplier is null ? NotFound() : Ok(supplier);
    }

    [HttpPost]
    public async Task<ActionResult<SupplierDto>> CreateSupplier(CreateSupplierRequest request)
    {
        var created = await supplierService.CreateSupplierAsync(request);
        return CreatedAtAction(nameof(GetSupplier), new { id = created.SupplierId }, created);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<SupplierDto>> UpdateSupplier(int id, UpdateSupplierRequest request)
    {
        var updated = await supplierService.UpdateSupplierAsync(id, request);
        return Ok(updated);
    }

    [HttpPatch("{id:int}/deactivate")]
    public async Task<IActionResult> DeactivateSupplier(int id)
    {
        await supplierService.DeactivateSupplierAsync(id);
        return NoContent();
    }
}
