namespace Core.Entities;

/// <summary>
/// One ordered line of a <see cref="SupplierRequest"/>. Unit cost is snapshotted at submission so
/// a later catalogue price edit never rewrites order history (CLAUDE.md architecture principle #8,
/// the same rule RequestItems.UnitCostSnapshot follows).
/// </summary>
public class SupplierRequestItem
{
    public int Id { get; set; }

    public int SupplierRequestId { get; set; }

    public SupplierRequest? SupplierRequest { get; set; }

    public int ItemId { get; set; }

    public StationeryItem? Item { get; set; }

    /// <summary>Always &gt; 0 — enforced by validator and by a check constraint.</summary>
    public int Quantity { get; set; }

    public decimal UnitCostSnapshot { get; set; }

    /// <summary>
    /// Stored rather than computed, matching the decision already recorded for
    /// RequestItems.LineTotal in AI_usage_report.md (2026-08-17): keeping
    /// Quantity * UnitCostSnapshot in step is the service layer's job.
    /// </summary>
    public decimal LineTotal { get; set; }
}
