namespace Core.Entities;

/// <summary>
/// A stationery request raised by an employee (requestor) to their superior (approver).
/// This is the Plan's employee-to-manager request workflow (M3/M4, §3.4, §4.2).
///
/// IMPORTANT — this is NOT the <c>SupplierRequest</c> table, which is the inventory team
/// ordering from suppliers. This models an employee asking for stationery. The two are
/// independent domains (different statuses, lifecycles, actors).
///
/// Header/line split mirrors the Plan's design (Plan §3.4). Total cost is snapshotted at
/// submission so price edits never rewrite history (CLAUDE.md principle #8).
///
/// State changes are atomic: Status + RequestStatusHistory + both notification rows
/// commit or roll back together (CLAUDE.md principle #6). Only <c>RequestState</c>
/// may transition Status; never UPDATE it directly.
/// </summary>
public class Request
{
    public int Id { get; set; }

    /// <summary>
    /// FK to Infrastructure.Identity.ApplicationUser.Id (AspNetUsers) — the employee who
    /// submitted the request. Core stays framework-independent so there is no navigation property;
    /// Infrastructure's EF configuration maps the relationship.
    /// </summary>
    public int RequestorEmployeeNumber { get; set; }

    /// <summary>
    /// FK to Infrastructure.Identity.ApplicationUser.Id (AspNetUsers) — the employee who must
    /// approve or reject (usually the requestor's superior). Nullable for workflow edge cases.
    /// </summary>
    public int? ApproverEmployeeNumber { get; set; }

    /// <summary>
    /// Status values from the approval workflow diagram:
    /// - Pending: just submitted, awaiting approver decision
    /// - Approved: approver approved (all lines approved)
    /// - PartiallyApproved: approver approved some lines, rejected others
    /// - Rejected: approver rejected all lines
    /// - Withdrawn: requestor withdrew before approval
    /// - CancellationPending: requestor requested cancellation of approved order
    /// - Cancelled: approver approved the cancellation
    /// - Fulfilled: all lines fulfilled (stock already moved, M4+)
    ///
    /// The single source of truth; never UPDATE directly, only via RequestStateMachine.Transition().
    /// </summary>
    public string Status { get; set; } = "Pending";

    /// <summary>
    /// Sum of the line totals, snapshotted at submission (CLAUDE.md principle #8).
    /// Recalculating on price changes is forbidden to preserve history.
    /// </summary>
    public decimal TotalEstimatedCost { get; set; }

    /// <summary>User-supplied delivery deadline (Plan §3.4).</summary>
    public DateTime? RequiredByDate { get; set; }

    /// <summary>Approver's reason for approval, rejection, or partial approval (Plan §4.2 notification §5).</summary>
    public string? DecisionComment { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Timestamp of the most recent status change (approved/rejected/etc.).</summary>
    public DateTime? DecidedAtUtc { get; set; }

    /// <summary>
    /// Concurrency token: app-managed Guid. Compare-then-set check before any update
    /// (CLAUDE.md principle #4 / m2 plan §2.2). On mismatch, return 409 Conflict.
    /// </summary>
    public Guid RowVersion { get; set; } = Guid.NewGuid();

    /// <summary>Line items in this request (Plan §3.4).</summary>
    public List<RequestItem> Items { get; set; } = [];

    /// <summary>Status changes and approval decisions (Plan §3.5 / 4.2).</summary>
    public List<RequestStatusHistory> StatusHistory { get; set; } = [];
}
