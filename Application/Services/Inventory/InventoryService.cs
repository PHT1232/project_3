using Application.DTOs.Inventory;
using Application.Interfaces.Inventory;
using FluentValidation;

namespace Application.Services.Inventory;

public class InventoryService(
    IInventoryQueries inventoryQueries,
    IStockService stockService,
    IStockQueries stockQueries,
    IValidator<AdjustStockRequest> adjustValidator) : IInventoryService
{
    public async Task<InventoryPageResult> GetInventoryAsync(int page, int pageSize)
    {
        var pageResult = await inventoryQueries.GetPagedAsync(page, pageSize);
        var summary = await inventoryQueries.GetSummaryAsync();
        return new InventoryPageResult(pageResult, summary);
    }

    public Task<IReadOnlyList<InventoryRowDto>> GetLowStockAsync() => inventoryQueries.GetLowStockAsync();

    public async Task<InventoryRowDto> AdjustStockAsync(int itemId, AdjustStockRequest request, int actorEmployeeNumber)
    {
        await adjustValidator.ValidateAndThrowAsync(request);
        return await stockService.AdjustAsync(
            itemId, request.ChangeQuantity, request.Reason, actorEmployeeNumber, request.RowVersion);
    }

    public Task<IReadOnlyList<StockTransactionDto>> GetTransactionHistoryAsync(int itemId) =>
        stockQueries.GetHistoryAsync(itemId);
}
