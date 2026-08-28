namespace Application.DTOs.SupplierRequests;

/// <summary>
/// The inventory cart as submitted by the client.
///
/// Named ...Command rather than CreateSupplierRequest because
/// <c>Application.DTOs.Suppliers.CreateSupplierRequest</c> already exists and means something
/// completely different — it creates a *Supplier*. Keep the two apart.
///
/// The client sends item ids and quantities only. Unit cost, item name, and (where the item has a
/// preferred supplier) the supplier are all resolved server-side from the database — the client is
/// never trusted for them.
/// </summary>
public sealed record CreateSupplierRequestCommand(IReadOnlyList<SupplierRequestLineInput> Items);

/// <param name="ItemId">Must exist and be active.</param>
/// <param name="Quantity">Must be greater than zero.</param>
/// <param name="SupplierId">
/// Only consulted when the item has no preferred <c>StationeryItem.SupplierId</c>. When the item
/// does have one, that wins and this is ignored — a client must not be able to redirect an order
/// to an arbitrary supplier. When the item has none, this is required, and the supplier must exist
/// and be active.
/// </param>
public sealed record SupplierRequestLineInput(int ItemId, int Quantity, int? SupplierId);
