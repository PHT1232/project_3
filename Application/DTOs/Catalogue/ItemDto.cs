namespace Application.DTOs.Catalogue;

public sealed record ItemDto(
    int ItemId,
    string ItemName,
    int CategoryId,
    string CategoryName,
    string UnitOfMeasure,
    decimal UnitCost,
    int QuantityAvailable,
    int ReorderLevel,
    int MinRankLevelToRequest,
    bool IsActive,
    int? SupplierId,
    string? SupplierName,
    Guid RowVersion);
