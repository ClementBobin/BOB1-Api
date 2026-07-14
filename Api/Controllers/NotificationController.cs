namespace Api.Controllers;

using Application.Interfaces;

using Domain.Dto;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Route("api/notifications")]
[Authorize]
public class NotificationController : BaseController
{
    private readonly INotificationService _notifications;

    public NotificationController(INotificationService notifications)
        => _notifications = notifications;

    /// <summary>GET /api/notifications — active, non-expired notifications for current user</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<NotificationDto>>> GetAll()
        => Ok(await _notifications.GetByUserAsync(CurrentUserId));

    /// <summary>GET /api/notifications/startup — notifications to display at app startup</summary>
    [HttpGet("startup")]
    public async Task<ActionResult<IEnumerable<NotificationDto>>> GetStartup()
        => Ok(await _notifications.GetStartupNotificationsAsync(CurrentUserId));

    /// <summary>GET /api/notifications/unread-count</summary>
    [HttpGet("unread-count")]
    public async Task<ActionResult<int>> UnreadCount()
        => Ok(await _notifications.GetUnreadCountAsync(CurrentUserId));

    /// <summary>POST /api/notifications/{id}/read</summary>
    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id)
    {
        await _notifications.MarkReadAsync(id, CurrentUserId);
        return NoContent();
    }

    /// <summary>POST /api/notifications/read-all</summary>
    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead()
    {
        await _notifications.MarkAllReadAsync(CurrentUserId);
        return NoContent();
    }
}
