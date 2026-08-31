namespace Core.Entities;

/// <summary>
/// Audit trail for request status transitions. Every status change (Submitted, Approved,
/// Rejected, etc.) writes exactly one row, preserving the sequence and the actor's comment.
///
/// Used to answer "who approved/rejected when" and to notify participants at each step
/// (Plan §3.5 events; Plan §4.2 notification triggers).
///
/// Append-only; never deleted or updated. Multiple rows per request are expected
/// (e.g., Submitted → Rejected → Withdrawn → Resubmitted → Approved).
/// </summary>
public class RequestStatusHistory
{
    public int Id { get; set; }

    public int RequestId { get; set; }

    public Request? Request { get; set; }

    /// <summary>The status being transitioned FROM (may be null for the initial "created" event).</summary>
    public string? FromStatus { get; set; }

    /// <summary>The new status being transitioned TO.</summary>
    public string ToStatus { get; set; } = string.Empty;

    /// <summary>
    /// FK to Infrastructure.Identity.ApplicationUser.Id (AspNetUsers) — the user who made
    /// the transition (usually the approver, sometimes the requestor for withdraw/cancel).
    /// Core stays framework-independent so there is no navigation property; Infrastructure's
    /// EF configuration maps the relationship.
    /// </summary>
    public int ActorEmployeeNumber { get; set; }

    /// <summary>
    /// Approver's comment justifying the decision (approval reason, rejection reason, etc.)
    /// — Plan §3.5 event payload.
    /// </summary>
    public string? Comment { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
