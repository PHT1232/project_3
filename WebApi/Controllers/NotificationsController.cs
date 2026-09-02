using Application.DTOs.Common;
using Application.DTOs.Notifications;
using Application.Interfaces.Auth;
using Application.Interfaces.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

/// <summary>
/// Notification feed endpoints (Plan §4.2 [SPEC], "Notifications — Member 4").
///
/// Accessible to any authenticated user, scoped to their own notifications — ownership is
/// enforced inside the query/service layer, not here (CLAUDE.md principle #9).
/// </summary>
[ApiController]
[Route("api/v1/notifications")]
[Authorize]
public class NotificationsController(
    INotificationQueries notificationQueries,
    INotificationService notificationService,
    ICurrentUserService currentUserService
) : ControllerBase
{
    /// <summary>Paged feed, newest first.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<NotificationDto>>> GetFeed(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var actor = currentUserService.EmployeeNumber
            ?? throw new InvalidOperationException("Authenticated request missing employee number claim.");

        var result = await notificationQueries.GetForUserAsync(actor, page, pageSize);
        return Ok(result);
    }

    /// <summary>
    /// Polled every 30s by the frontend bell (Plan §3.3: "must be a single indexed COUNT").
    /// </summary>
    [HttpGet("unread-count")]
    public async Task<ActionResult<UnreadCountDto>> GetUnreadCount()
    {
        var actor = currentUserService.EmployeeNumber
            ?? throw new InvalidOperationException("Authenticated request missing employee number claim.");

        var count = await notificationQueries.GetUnreadCountAsync(actor);
        return Ok(new UnreadCountDto(count));
    }

    /// <summary>Marks a single notification read. 404 if it doesn't exist or isn't the caller's.</summary>
    [HttpPost("{id:long}/read")]
    public async Task<IActionResult> MarkRead(long id)
    {
        var actor = currentUserService.EmployeeNumber
            ?? throw new InvalidOperationException("Authenticated request missing employee number claim.");

        var marked = await notificationService.MarkReadAsync(id, actor);
        return marked ? NoContent() : NotFound();
    }

    /// <summary>Marks every one of the caller's unread notifications as read.</summary>
    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead()
    {
        var actor = currentUserService.EmployeeNumber
            ?? throw new InvalidOperationException("Authenticated request missing employee number claim.");

        await notificationService.MarkAllReadAsync(actor);
        return NoContent();
    }
}
