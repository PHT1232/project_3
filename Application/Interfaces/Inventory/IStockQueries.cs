using Application.DTOs.Inventory;

namespace Application.Interfaces.Inventory;

/// <summary>Transaction history reads; CreatedByName joins ApplicationUser (see m2 plan §2.3, K8).</summary>
public interface IStockQueries
{
    Task<IReadOnlyList<StockTransactionDto>> GetHistoryAsync(int itemId);
}
