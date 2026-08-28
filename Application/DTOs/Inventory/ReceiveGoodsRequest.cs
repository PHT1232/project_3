namespace Application.DTOs.Inventory;

public sealed record ReceiveGoodsRequest(int Quantity, int? SupplierId, string? Reference, Guid RowVersion);
