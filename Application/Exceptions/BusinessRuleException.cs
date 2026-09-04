namespace Application.Exceptions;

/// <summary>
/// A request was well-formed and the caller was allowed to make it, but it violates a domain
/// rule — the Plan's 422 case (§4.2 error table: "Business rule violation · Request total
/// exceeds role threshold").
///
/// Distinct from the other three on purpose: <c>ValidationException</c> (400) means the payload
/// itself is malformed, <c>NotFoundException</c> (404) means the row is absent or invisible, and
/// <c>ConflictException</c> (409) means the state or concurrency token moved underneath the
/// caller. This one means "we understood you, the data is fine, and the answer is still no" —
/// which is exactly what an over-budget submission or an under-stocked approval is.
/// </summary>
public class BusinessRuleException(string message) : Exception(message);
