using Application.DTOs.Inventory;
using Application.Interfaces.Auth;
using Application.Interfaces.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[ApiController]
[Route("api/v1/inventory")]
[Authorize(Policy = "RequireManager")]
public class InventoryController(IInventoryService inventoryService, ICurrentUserService currentUserService)
    : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<InventoryPageResult>> GetInventory(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await inventoryService.GetInventoryAsync(page, pageSize);
        return Ok(result);
    }

    [HttpGet("low-stock")]
    public async Task<ActionResult<IReadOnlyList<InventoryRowDto>>> GetLowStock()
    {
        var rows = await inventoryService.GetLowStockAsync();
        return Ok(rows);
    }

    [HttpPost("{itemId:int}/adjust")]
    public async Task<ActionResult<InventoryRowDto>> AdjustStock(int itemId, AdjustStockRequest request)
    {
        var actor = currentUserService.EmployeeNumber
            ?? throw new InvalidOperationException("Authenticated request missing employee number claim.");
        var updated = await inventoryService.AdjustStockAsync(itemId, request, actor);
        return Ok(updated);
    }

    // POST {itemId}/receive was removed on 2026-09-04. It raised the balance the moment stock was
    // "recorded", so the system showed goods as available before they had physically arrived, and
    // any Manager could do it. Receipts now happen only through
    // POST /api/v1/supplier-requests/{id}/confirm-arrival, where a Business Manager confirms a
    // real delivery against a real order. Corrections still go through {itemId}/adjust.

    [HttpGet("{itemId:int}/transactions")]
    public async Task<ActionResult<IReadOnlyList<StockTransactionDto>>> GetTransactions(int itemId)
    {
        var history = await inventoryService.GetTransactionHistoryAsync(itemId);
        return Ok(history);
    }
}
