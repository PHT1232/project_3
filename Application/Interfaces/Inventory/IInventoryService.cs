using Application.DTOs.Inventory;

namespace Application.Interfaces.Inventory;

public interface IInventoryService
{
    Task<InventoryPageResult> GetInventoryAsync(int page, int pageSize);

    Task<IReadOnlyList<InventoryRowDto>> GetLowStockAsync();

    Task<InventoryRowDto> AdjustStockAsync(int itemId, AdjustStockRequest request, int actorEmployeeNumber);

    Task<InventoryRowDto> ReceiveGoodsAsync(int itemId, ReceiveGoodsRequest request, int actorEmployeeNumber);

    Task<IReadOnlyList<StockTransactionDto>> GetTransactionHistoryAsync(int itemId);
}
