namespace Application.DTOs.Inventory;

public sealed record InventorySummaryDto(int TotalItems, int LowStockAlerts, decimal TotalValue);
