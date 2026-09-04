namespace Core.Entities;

/// <summary>
/// Append-only ledger row — never updated or deleted. QuantityAvailable on StationeryItem is a
/// cached balance; this table is the source of truth (Plan §2, m2 plan §2.2).
/// </summary>
public class StockTransaction
{
    public int Id { get; set; }

    public int ItemId { get; set; }

    public StationeryItem? Item { get; set; }

    public StockTransactionType TxType { get; set; }

    /// <summary>Signed: negative for Issue, positive for Receipt, either for Adjustment. Never 0.</summary>
    public int ChangeQuantity { get; set; }

    public decimal UnitCostSnapshot { get; set; }

    public string? Reference { get; set; }

    /// <summary>
    /// FK to Request.Id when this movement was caused by a stationery request — the Issue rows
    /// written when an approver approves it, and the Adjustment rows written when an approved
    /// request is later cancelled. Null for goods receipts and manual adjustments, which have
    /// no request behind them. Lets the ledger answer "which request took this stock?" instead
    /// of leaving only the free-text <see cref="Reference"/>.
    /// </summary>
    public int? RequestId { get; set; }

    public int? SupplierId { get; set; }

    public Supplier? Supplier { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// FK to Infrastructure.Identity.ApplicationUser.Id (AspNetUsers) — Core stays
    /// framework-independent, so no navigation property here; Infrastructure's EF
    /// configuration maps the relationship. See m2 plan §2.3 (K8 follow-up).
    /// </summary>
    public int CreatedByEmployeeNumber { get; set; }
}
