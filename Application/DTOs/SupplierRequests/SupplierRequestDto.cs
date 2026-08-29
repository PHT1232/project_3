namespace Application.DTOs.SupplierRequests;

/// <summary>
/// One created order, already grouped by supplier. A single cart submission returns a list of
/// these — one per distinct supplier in the cart.
/// </summary>
public sealed record SupplierRequestDto(
    int SupplierRequestId,
    int SupplierId,
    string SupplierName,
    decimal TotalCost,
    DateTime CreatedAtUtc,
    int CreatedByEmployeeNumber,
    IReadOnlyList<SupplierRequestLineDto> Items);

public sealed record SupplierRequestLineDto(
    int ItemId,
    string ItemName,
    int Quantity,
    decimal UnitCostSnapshot,
    decimal LineTotal);
