using Application.DTOs.Inventory;
using Core.Entities;

namespace Application.Interfaces.Inventory;

/// <summary>
/// The single write path onto the stock ledger (CLAUDE.md architecture principle #5 — every
/// balance change writes a matching ledger row in the same transaction). `expectedRowVersion`
/// is the token the caller last read; a mismatch against the current row throws ConflictException
/// (409) rather than silently overwriting a concurrent change (m2 plan §4.5).
///
/// Two of the methods below stage without saving. Both exist because their callers must commit
/// the movement together with something else — a request's status change, or a supplier order's
/// arrival — and they are kept separate because the rules differ: a request movement can be
/// negative and must refuse to go below zero; a supplier receipt is always positive and carries a
/// supplier rather than a request.
/// </summary>
public interface IStockService
{
    /// <summary>
    /// Standalone issue: checks the caller's row version, writes the ledger row and commits on
    /// its own. Not called by any endpoint — request approval uses
    /// <see cref="StageRequestMovementAsync"/> instead, because it must commit together with the
    /// status change, history row and notifications.
    /// </summary>
    Task<InventoryRowDto> IssueAsync(int itemId, int quantity, int actorEmployeeNumber, string reference, Guid expectedRowVersion);

    /// <summary>
    /// Stages one request-driven stock movement **without saving** — the balance change and its
    /// ledger row are left pending on the tracked context so the caller can commit them in the
    /// same <c>SaveChangesAsync</c> as the request's status, history and notifications
    /// (Plan §3.6: "one DB transaction"; CLAUDE.md principle #6).
    ///
    /// <paramref name="changeQuantity"/> is negative to issue stock on approval and positive to
    /// restore it when an approved request is cancelled. There is no <c>expectedRowVersion</c>
    /// parameter: the approval already gated on the *request's* row version, and re-checking
    /// each item's would make an approval fail because someone unrelated received stock.
    ///
    /// Throws <see cref="Application.Exceptions.BusinessRuleException"/> (422) if the movement
    /// would drive the balance below zero. Callers should pre-check every line first so a
    /// multi-line approval fails before it has staged anything.
    /// </summary>
    Task StageRequestMovementAsync(
        int itemId,
        int changeQuantity,
        StockTransactionType txType,
        int requestId,
        string reference,
        int actorEmployeeNumber);

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
