using Application.DTOs.Common;
using Application.DTOs.Inventory;

namespace Application.Interfaces.Inventory;

/// <summary>
/// Joins StationeryItem + derives Status. Status thresholds are an implementation choice, not
/// specified by the plan: REORDER_NOW when QuantityAvailable &lt;= ReorderLevel, WATCH when
/// QuantityAvailable &lt;= ReorderLevel * 1.5, OK otherwise. A consumption-rate/lead-time-demand
/// model (as the frontend mock's comment describes) is explicitly M5 AI territory, not this.
/// </summary>
public interface IInventoryQueries
{
    Task<PagedResult<InventoryRowDto>> GetPagedAsync(int page, int pageSize);

    Task<InventorySummaryDto> GetSummaryAsync();

    Task<IReadOnlyList<InventoryRowDto>> GetLowStockAsync();

    Task<InventoryRowDto?> GetRowAsync(int itemId);
}
