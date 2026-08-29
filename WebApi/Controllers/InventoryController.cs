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

    [HttpPost("{itemId:int}/receive")]
    public async Task<ActionResult<InventoryRowDto>> ReceiveGoods(int itemId, ReceiveGoodsRequest request)
    {
        var actor = currentUserService.EmployeeNumber
            ?? throw new InvalidOperationException("Authenticated request missing employee number claim.");
        var updated = await inventoryService.ReceiveGoodsAsync(itemId, request, actor);
        return Ok(updated);
    }

    [HttpGet("{itemId:int}/transactions")]
    public async Task<ActionResult<IReadOnlyList<StockTransactionDto>>> GetTransactions(int itemId)
    {
        var history = await inventoryService.GetTransactionHistoryAsync(itemId);
        return Ok(history);
    }
}
