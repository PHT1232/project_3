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
    IReadOnlyList<SupplierRequestLineDto> Items,
    /// <summary>"PendingArrival" until a Business Manager confirms arrival, then "Received".</summary>
    string Status,
    DateTime? ReceivedAtUtc,
    int? ReceivedByEmployeeNumber,
    string? ReceivedByName);

public sealed record SupplierRequestLineDto(
    int ItemId,
    string ItemName,
    int Quantity,
    decimal UnitCostSnapshot,
    decimal LineTotal);
