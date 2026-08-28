namespace Application.DTOs.Inventory;

public sealed record StockTransactionDto(
    int TransactionId,
    int ItemId,
    string TxType,
    int ChangeQuantity,
    decimal UnitCostSnapshot,
    string? Reference,
    int? SupplierId,
    DateTime CreatedAtUtc,
    int CreatedByEmployeeNumber,
    string CreatedByName);
