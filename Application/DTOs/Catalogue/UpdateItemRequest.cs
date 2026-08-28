namespace Application.DTOs.Catalogue;

/// <summary>RowVersion is the token the client last saw (from ItemDto) — required so the
/// server can detect a stale edit (m2 plan §4.5).</summary>
public sealed record UpdateItemRequest(
    string ItemName,
    int CategoryId,
    string UnitOfMeasure,
    decimal UnitCost,
    int ReorderLevel,
    int MinRankLevelToRequest,
    int? SupplierId,
    Guid RowVersion);
