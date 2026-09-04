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
/// when a Business Manager confirms the goods physically arrived — see <see cref="Status"/>.
///
/// The lifecycle below was specified by the team on 2026-09-04, resolving the open question this
/// comment previously recorded ("no project document specifies the states a supplier order moves
/// through … when the team specifies a lifecycle, it is an additive migration"). It is exactly
/// that additive migration; no other status vocabulary was invented.
/// </summary>
public class SupplierRequest
{
    /// <summary>Ordered from the supplier; goods have not physically arrived. No stock yet.</summary>
    public const string StatusPendingArrival = "PendingArrival";

    /// <summary>A Business Manager confirmed arrival; the stock receipt has been posted.</summary>
    public const string StatusReceived = "Received";

    public int Id { get; set; }

    public int SupplierId { get; set; }

    public Supplier? Supplier { get; set; }

    /// <summary>Sum of the line totals, snapshotted at submission (CLAUDE.md principle #8).</summary>
    public decimal TotalCost { get; set; }

    /// <summary>
    /// <see cref="StatusPendingArrival"/> or <see cref="StatusReceived"/>. Every order starts
    /// Pending Arrival. Only the transition to Received posts stock, and only once — the guard
    /// is the status check in <c>SupplierRequestService.ConfirmArrivalAsync</c>, which runs in
    /// the same transaction as the receipt rows.
    /// </summary>
    public string Status { get; set; } = StatusPendingArrival;

    /// <summary>When arrival was confirmed. Null while Pending Arrival.</summary>
    public DateTime? ReceivedAtUtc { get; set; }

    /// <summary>
    /// FK to ApplicationUser.Id — the Business Manager who confirmed arrival. Null while
    /// Pending Arrival. Scalar only, same pattern as <see cref="CreatedByEmployeeNumber"/>.
    /// </summary>
    public int? ReceivedByEmployeeNumber { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// FK to Infrastructure.Identity.ApplicationUser.Id (AspNetUsers). Core stays
    /// framework-independent, so there is no navigation property here — Infrastructure's EF
    /// configuration maps the relationship, the same way StockTransaction does.
    /// </summary>
    public int CreatedByEmployeeNumber { get; set; }

    public List<SupplierRequestItem> Items { get; set; } = [];
}
