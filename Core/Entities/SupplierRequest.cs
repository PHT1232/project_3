namespace Core.Entities;

/// <summary>
/// A replenishment order raised against one supplier — the header of a header/line pair, mirroring
/// the Plan's Requests/RequestItems split (Plan §3.4).
///
/// IMPORTANT — this is NOT the Plan's <c>Requests</c> table. That models an employee asking their
/// superior for stationery (M3/M4, not yet built). This models the inventory team ordering stock
/// from a supplier. The two are different domains and must not be merged.
///
/// Creating one of these does NOT move stock. Stock changes only through <c>IStockService</c>
/// when the goods are actually received (CLAUDE.md architecture principle #5).
///
/// There is deliberately no status/lifecycle column: no project document specifies the states a
/// supplier order moves through, and inventing them is exactly what K3 flagged. When the team
/// specifies a lifecycle, it is an additive migration.
/// </summary>
public class SupplierRequest
{
    public int Id { get; set; }

    public int SupplierId { get; set; }

    public Supplier? Supplier { get; set; }

    /// <summary>Sum of the line totals, snapshotted at submission (CLAUDE.md principle #8).</summary>
    public decimal TotalCost { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// FK to Infrastructure.Identity.ApplicationUser.Id (AspNetUsers). Core stays
    /// framework-independent, so there is no navigation property here — Infrastructure's EF
    /// configuration maps the relationship, the same way StockTransaction does.
    /// </summary>
    public int CreatedByEmployeeNumber { get; set; }

    public List<SupplierRequestItem> Items { get; set; } = [];
}
