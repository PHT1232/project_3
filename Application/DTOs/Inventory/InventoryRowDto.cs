namespace Application.DTOs.Inventory;

/// <summary>
/// Status is a plain string ("OK"/"WATCH"/"REORDER_NOW") matching the frontend's existing
/// INVENTORY_STATUS constants exactly, rather than an enum + JSON converter.
/// </summary>
/// <param name="SupplierId">
/// The item's preferred supplier, or null when it has none. Additive — the inventory cart shows
/// it per row, and a null means the user must choose a supplier for that line before submitting.
/// </param>
public sealed record InventoryRowDto(
    int ItemId,
    string ItemName,
    int QuantityAvailable,
    int ReorderLevel,
    decimal UnitCost,
    string Status,
    Guid RowVersion,
    int? SupplierId = null,
    string? SupplierName = null);
