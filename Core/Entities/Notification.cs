using Core.Enums;

namespace Core.Entities;

/// <summary>
/// Persisted notification feed row (Plan §3.3 table 10, §4.2 [SPEC]). Every one of the 6
/// trigger events writes exactly 2 rows — one per recipient — never a single shared row.
///
/// Append-only except for IsRead, which the recipient flips via
/// POST /api/v1/notifications/{id}/read or /read-all. Never deleted.
/// </summary>
public class Notification
{
    public long Id { get; set; }

    /// <summary>
    /// FK to Infrastructure.Identity.ApplicationUser.Id (AspNetUsers) — Core stays
    /// framework-independent, so no navigation property here; Infrastructure's EF
    /// configuration maps the relationship (same pattern as StockTransaction/RequestStatusHistory).
    /// </summary>
    public int RecipientEmployeeNumber { get; set; }

    /// <summary>Null for triggers not tied to a request (currently just PasswordChanged).</summary>
    public int? RequestId { get; set; }

    public Request? Request { get; set; }

    public NotificationEventType EventType { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public bool IsRead { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
