namespace Core.Entities;

/// <summary>
/// A help/bug message a user sends from the Help page (Option B of the "contact the team"
/// decision — an in-app inbox instead of SMTP, which is on the Plan's [CUT] list). Stored
/// here and triaged by Manager+ in the Support Inbox screen; the app never sends email.
///
/// Only <see cref="Status"/>, <see cref="ResolvedAtUtc"/> and <see cref="ResolvedByEmployeeNumber"/>
/// are ever updated (when a manager resolves or reopens it) — the message body is immutable.
/// </summary>
public class SupportMessage
{
    public int Id { get; set; }

    /// <summary>
    /// FK to Infrastructure.Identity.ApplicationUser.Id (AspNetUsers) — who sent it. Core is
    /// framework-independent, so no navigation property (same pattern as Notification).
    /// </summary>
    public int SenderEmployeeNumber { get; set; }

    /// <summary>Feature area the sender picked, e.g. "Approvals" (free text; UI constrains it).</summary>
    public string Area { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    /// <summary>Client context captured at send time (app version, page, browser). Optional.</summary>
    public string? Diagnostics { get; set; }

    /// <summary>"New" or "Resolved". Never UPDATE except via the service.</summary>
    public string Status { get; set; } = SupportMessageStatus.New;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? ResolvedAtUtc { get; set; }

    /// <summary>FK to AspNetUsers.Id — the manager who resolved it. Null while New.</summary>
    public int? ResolvedByEmployeeNumber { get; set; }
}

public static class SupportMessageStatus
{
    public const string New = "New";
    public const string Resolved = "Resolved";

    public static readonly string[] All = [New, Resolved];
}
