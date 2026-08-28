namespace Application.DTOs.Inventory;

/// <summary>
/// Status is a plain string ("OK"/"WATCH"/"REORDER_NOW") matching the frontend's existing
/// INVENTORY_STATUS constants exactly, rather than an enum + JSON converter.
/// </summary>
public sealed record InventoryRowDto(
    int ItemId,
    string ItemName,
    int QuantityAvailable,
    int ReorderLevel,
    decimal UnitCost,
    string Status,
    Guid RowVersion);
