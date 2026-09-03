namespace Application.DTOs.Ai;

/// <summary>
/// Output of the request assistant — an editable draft, never a persisted request
/// (Plan §5.2 rule 1). Line fields mirror what the New Request page already needs to build a
/// requisition line, so the frontend can merge it into its existing state without a lookup.
/// </summary>
public sealed record DraftRequestDto(
    IReadOnlyList<DraftRequestItemDto> Items,
    DateTime? RequiredByDate,
    string? Note,
    decimal TotalEstimatedCost,
    IReadOnlyList<string> Warnings,
    bool WasFallback,
    string Model);

public sealed record DraftRequestItemDto(
    int ItemId,
    string ItemName,
    string CategoryName,
    string? SupplierName,
    string UnitOfMeasure,
    decimal UnitCost,
    int Quantity,
    int QuantityAvailable);
