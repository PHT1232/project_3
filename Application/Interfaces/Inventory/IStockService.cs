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

    // ReceiveAsync (standalone, immediate receipt) was removed on 2026-09-04 along with
    // POST /inventory/{itemId}/receive: a receipt must be tied to a confirmed supplier-order
    // arrival, so StageReceiptAsync below is the only way to post one.

    /// <summary>
    /// Stages a Receipt for one line of a supplier order — updates the cached balance and adds the
    /// ledger row, but does NOT call SaveChangesAsync. The caller saves, so a multi-line arrival
    /// confirmation commits every line plus the order's status change together or not at all
    /// (same "stage, caller saves" contract as INotificationService.NotifyRequestEventAsync).
    ///
    /// No RowVersion argument: the actor is confirming a delivery against an order, not editing a
    /// specific item revision, so there is no stale-edit to detect. The item's RowVersion is still
    /// rotated so concurrent item editors see a conflict.
    /// </summary>
    Task StageReceiptAsync(int itemId, int quantity, int? supplierId, string? reference, int actorEmployeeNumber);

    Task<InventoryRowDto> AdjustAsync(int itemId, int changeQuantity, string reason, int actorEmployeeNumber, Guid expectedRowVersion);
}
