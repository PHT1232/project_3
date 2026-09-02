using Application.DTOs.Common;
using Application.DTOs.Notifications;

namespace Application.Interfaces.Notifications;

public interface INotificationQueries
{
    /// <summary>Newest first.</summary>
    Task<PagedResult<NotificationDto>> GetForUserAsync(int employeeNumber, int page, int pageSize);

    /// <summary>Plan §3.3: "must be a single indexed COUNT" — backed by IX (RecipientEmployeeNumber, IsRead).</summary>
    Task<int> GetUnreadCountAsync(int employeeNumber);
}
