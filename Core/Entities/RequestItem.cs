namespace Core.Entities;

/// <summary>
/// Line item in a stationery request — the "<c>RequestItems</c>" table from the Plan (§3.4).
///
/// Unit cost is snapshotted at submission so subsequent price changes do not alter the history
/// of what was actually requested (CLAUDE.md principle #8). LineTotal is derived/stored
/// (Quantity × UnitCostSnapshot) — keep them in sync in Application services.
///
/// Only the approver's decision on the entire Request moves stock; this line itself never
/// writes to StockTransactions (that happens in M4 fulfillment, via <c>IStockService.IssueAsync</c>).
/// </summary>
public class RequestItem
{
    public int Id { get; set; }

    public int RequestId { get; set; }

    public Request? Request { get; set; }

    /// <summary>
    /// FK to StationeryItem.Id — the item being requested. Cannot be null; every line references
    /// a real catalogue item.
    /// </summary>
    public int ItemId { get; set; }

    public StationeryItem? Item { get; set; }

    /// <summary>Quantity requested (validated > 0).</summary>
    public int Quantity { get; set; }

    /// <summary>
    /// Unit price of the item at the time of submission. Subsequent edits to StationeryItem.UnitCost
    /// do not alter this snapshot (CLAUDE.md principle #8).
    /// </summary>
    public decimal UnitCostSnapshot { get; set; }

    /// <summary>Quantity × UnitCostSnapshot (stored, not computed — keep in sync).</summary>
    public decimal LineTotal { get; set; }
}
