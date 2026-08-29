namespace Application.DTOs.Inventory;

public sealed record AdjustStockRequest(int ChangeQuantity, string Reason, Guid RowVersion);
