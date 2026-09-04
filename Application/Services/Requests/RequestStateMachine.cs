using Application.Exceptions;
using Core.Entities;

namespace Application.Services.Requests;

/// <summary>
/// The one guarded writer of <see cref="Request.Status"/> (Plan T3.2, CLAUDE.md principle #7:
/// "Only <c>RequestStateMachine.Transition()</c> may write <c>Request.Status</c>").
///
/// Before this existed the transition rules were inline <c>if</c> checks spread across five
/// methods of <c>RequestService</c>. They were individually correct, but nothing stopped the
/// sixth method from forgetting one, and there was no single place to read the workflow off
/// (audit finding H3).
///
/// <b>What it does and does not do.</b> It owns the <i>shape</i> of the workflow: which status
/// may follow which, and the bookkeeping every transition shares — set the status, append the
/// audit row, rotate the row version. It deliberately does NOT own the guards that need data it
/// cannot see: ownership, RowVersion match, budget, stock. Those stay in the service, run before
/// the transition, and are what make a transition legal in context.
///
/// <b>Statuses.</b> Plan §3.6 minus two the team removed: <c>ReturnedForModification</c> is out
/// of scope by team decision (CLAUDE.md K1) and <c>Fulfilled</c> was dropped when approval itself
/// became the stock movement (audit C8). Both are absent from <c>CK_Requests_Status</c> too, so
/// this table and the database agree.
/// </summary>
public static class RequestStateMachine
{
    public const string Draft = "Draft";
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string PartiallyApproved = "PartiallyApproved";
    public const string Rejected = "Rejected";
    public const string Withdrawn = "Withdrawn";
    public const string CancellationPending = "CancellationPending";
    public const string Cancelled = "Cancelled";

    /// <summary>
    /// Every legal edge in Plan §3.6's state diagram. Anything not listed here throws.
    /// Keep this in step with <c>CK_Requests_Status</c> in RequestConfiguration.
    /// </summary>
    private static readonly Dictionary<string, string[]> Allowed = new()
    {
        [Draft] = [Pending],
        [Pending] = [Approved, PartiallyApproved, Rejected, Withdrawn],
        [Approved] = [CancellationPending],
        [PartiallyApproved] = [CancellationPending],
        // Approving the cancellation ends at Cancelled; refusing it returns the request to
        // whichever of the two approved states it came from.
        [CancellationPending] = [Cancelled, Approved, PartiallyApproved],
        // Terminal.
        [Rejected] = [],
        [Withdrawn] = [],
        [Cancelled] = [],
    };

    /// <summary>True when <paramref name="to"/> may directly follow <paramref name="from"/>.</summary>
    public static bool CanTransition(string from, string to) =>
        Allowed.TryGetValue(from, out var next) && next.Contains(to);

    /// <summary>The statuses that may follow <paramref name="from"/>; empty for a terminal state.</summary>
    public static IReadOnlyCollection<string> NextStatuses(string from) =>
        Allowed.TryGetValue(from, out var next) ? next : [];

    /// <summary>
    /// Moves <paramref name="request"/> to <paramref name="to"/>, appending the audit row and
    /// rotating the concurrency token. Throws <see cref="InvalidStateTransitionException"/>
    /// (→ 409) if the edge is not in Plan §3.6.
    ///
    /// Does not save: the caller commits this together with whatever else the transition implies
    /// — stock movements, notifications — in one <c>SaveChangesAsync</c> (CLAUDE.md principle #6).
    /// </summary>
    public static void Transition(
        Request request,
        string to,
        int actorEmployeeNumber,
        string? comment = null,
        DateTime? atUtc = null)
    {
        ArgumentNullException.ThrowIfNull(request);

        var from = request.Status;

        if (!Allowed.ContainsKey(from))
        {
            throw new InvalidStateTransitionException(from, to, $"'{from}' is not a known status.");
        }

        if (!CanTransition(from, to))
        {
            var options = NextStatuses(from);
            var allowed = options.Count == 0
                ? $"'{from}' is a final status."
                : $"from '{from}' a request may only become {string.Join(", ", options)}.";

            throw new InvalidStateTransitionException(from, to, allowed);
        }

        var timestamp = atUtc ?? DateTime.UtcNow;

        request.Status = to;
        request.RowVersion = Guid.NewGuid();

        request.StatusHistory.Add(new RequestStatusHistory
        {
            FromStatus = from,
            ToStatus = to,
            ActorEmployeeNumber = actorEmployeeNumber,
            Comment = comment,
            CreatedAtUtc = timestamp,
        });
    }
}
