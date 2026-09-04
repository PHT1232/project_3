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
/// Creating an order does not move stock. The order is recorded Pending Arrival; the balance
/// only rises when a Business Manager confirms the goods physically arrived through
/// POST /api/v1/supplier-requests/{id}/confirm-arrival.
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

    /// <summary>
    /// "Goods Arrived" — the Business Manager confirms the delivery physically turned up, which
    /// is the only thing that raises the stock balance for an order (PendingArrival → Received).
    ///
    /// Business Manager and above only (RankLevel >= 3), a deliberately narrower policy than the
    /// Manager+ default on this controller: a Manager may raise an order but may not certify that
    /// it arrived. Confirming twice returns 409 — the balance moves exactly once.
    /// </summary>
    [HttpPost("{id:int}/confirm-arrival")]
    [Authorize(Policy = "RequireBusinessManager")]
    public async Task<ActionResult<SupplierRequestDto>> ConfirmArrival(int id)
    {
        var actor = currentUserService.EmployeeNumber
            ?? throw new InvalidOperationException("Authenticated request missing employee number claim.");

        return Ok(await supplierRequestService.ConfirmArrivalAsync(id, actor));
    }
}
