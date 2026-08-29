namespace Application.DTOs.Catalogue;

public sealed record CreateItemRequest(
    string ItemName,
    int CategoryId,
    string UnitOfMeasure,
    decimal UnitCost,
    int ReorderLevel,
    int MinRankLevelToRequest,
    int? SupplierId);
