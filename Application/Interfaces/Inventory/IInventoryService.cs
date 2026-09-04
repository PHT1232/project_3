using Application.DTOs.Inventory;

namespace Application.Interfaces.Inventory;

public interface IInventoryService
{
    Task<InventoryPageResult> GetInventoryAsync(int page, int pageSize);

    Task<IReadOnlyList<InventoryRowDto>> GetLowStockAsync();

    Task<InventoryRowDto> AdjustStockAsync(int itemId, AdjustStockRequest request, int actorEmployeeNumber);

    // ReceiveGoodsAsync was removed on 2026-09-04 — goods are received by confirming arrival on a
    // supplier order (ISupplierRequestService.ConfirmArrivalAsync), never ad hoc per item.

    Task<IReadOnlyList<StockTransactionDto>> GetTransactionHistoryAsync(int itemId);
}
