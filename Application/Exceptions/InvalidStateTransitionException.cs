namespace Application.Exceptions;

/// <summary>
/// A request was asked to move between two statuses that Plan §3.6 does not connect — for
/// example approving a Draft, or withdrawing something already Cancelled.
///
/// Derives from <see cref="ConflictException"/> so it keeps mapping to <b>409 Conflict</b>: the
/// caller's view of the request is stale, which is the same thing a RowVersion mismatch means.
/// Callers that only care about "this could not be done right now" need no change; anything that
/// wants to single out an illegal transition can catch the specific type.
/// </summary>
public class InvalidStateTransitionException(string from, string to, string reason)
    : ConflictException($"Cannot move a request from {from} to {to}: {reason}")
{
    public string From { get; } = from;

    public string To { get; } = to;
}
