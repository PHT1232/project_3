using Application.DTOs.Inventory;

namespace Application.Interfaces.Inventory;

/// <summary>
/// The single write path onto the stock ledger (CLAUDE.md architecture principle #5 — every
/// balance change writes a matching ledger row in the same transaction). `expectedRowVersion`
/// is the token the caller last read; a mismatch against the current row throws ConflictException
/// (409) rather than silently overwriting a concurrent change (m2 plan §4.5).
/// </summary>
public interface IStockService
{
    /// <summary>Not called by any M2 endpoint yet — reserved for M4's request-fulfillment flow.</summary>
    Task<InventoryRowDto> IssueAsync(int itemId, int quantity, int actorEmployeeNumber, string reference, Guid expectedRowVersion);

    Task<InventoryRowDto> ReceiveAsync(int itemId, int quantity, int? supplierId, string? reference, int actorEmployeeNumber, Guid expectedRowVersion);

    Task<InventoryRowDto> AdjustAsync(int itemId, int changeQuantity, string reason, int actorEmployeeNumber, Guid expectedRowVersion);
}
