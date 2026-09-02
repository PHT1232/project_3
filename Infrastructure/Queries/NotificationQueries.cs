using Application.DTOs.Common;
using Application.DTOs.Notifications;
using Application.Interfaces.Notifications;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Queries;

public class NotificationQueries(DataContext db) : INotificationQueries
{
    public async Task<PagedResult<NotificationDto>> GetForUserAsync(int employeeNumber, int page, int pageSize)
    {
        var query = db.Notifications
            .AsNoTracking()
            .Where(n => n.RecipientEmployeeNumber == employeeNumber);

        var totalCount = await query.CountAsync();

        // .ToString() on the EventType enum isn't reliably translatable to SQL across
        // providers (SQL Server vs the SQLite in-memory test provider), so the entities are
        // loaded first and mapped to DTOs in memory rather than projected inside the query.
        var entities = await query
            .OrderByDescending(n => n.CreatedAtUtc)
            .ThenByDescending(n => n.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var items = entities
            .Select(n => new NotificationDto(
                n.Id,
                n.EventType.ToString(),
                n.Title,
                n.Message,
                n.IsRead,
                n.CreatedAtUtc,
                n.RequestId))
            .ToList();

        return new PagedResult<NotificationDto>(items, page, pageSize, totalCount);
    }

    public Task<int> GetUnreadCountAsync(int employeeNumber) =>
        db.Notifications.CountAsync(n => n.RecipientEmployeeNumber == employeeNumber && !n.IsRead);
}
