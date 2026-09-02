namespace Core.Enums;

/// <summary>
/// The 6 notification triggers named in Plan §4.2 ([SPEC]): request entered · approved ·
/// rejected · withdrawn · cancelled · password changed. Stored as int (matches the
/// StockTransactionType convention — see Infrastructure/Data/Configurations/
/// StockTransactionConfiguration.cs), not the nvarchar the Plan's illustrative ERD shows.
///
/// PartiallyApproved requests raise RequestApproved too — the Plan doesn't name a 7th,
/// separate "partially approved" trigger.
/// </summary>
public enum NotificationEventType
{
    RequestSubmitted = 0,
    RequestApproved = 1,
    RequestRejected = 2,
    RequestWithdrawn = 3,
    RequestCancelled = 4,
    PasswordChanged = 5,
}
