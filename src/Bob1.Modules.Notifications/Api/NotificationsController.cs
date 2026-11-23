using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bob1.Modules.Notifications.Api;

internal sealed record PushTokenRequest(string Token);
internal sealed record BroadcastRequest(string Title, string Body, Guid? DivisionId, Guid? TeamId = null, DateTime? ExpiresAtUtc = null, bool Recurring = false, bool Startup = false);
internal sealed record NotificationDto(Guid Id, string Title, string Body, string? DataJson, DateTime CreatedAtUtc, bool IsRead, DateTime? ExpiresAtUtc, bool Recurring, bool Startup);

internal sealed record MyNotificationQuery(bool? UnreadOnly, int? Skip, int? Take);

[Route(ApiRoutes.V1)]
[Authorize]
[Tags("Notifications")]
internal sealed class NotificationsController(NotificationsDbContext db, ICurrentUser user, TimeProvider clock) : ApiControllerBase
{
    // ---- Device tokens (FCM) ----

    [HttpPost("me/push-tokens")]
    public async Task<IActionResult> RegisterPushToken(PushTokenRequest r, CancellationToken ct)
    {
        if (Guard.NotBlank(r.Token, "token", 512) is { } err)
            return err.ToProblem();

        var now = clock.GetUtcNow().UtcDateTime;
        var token = r.Token.Trim();
        var existing = await db.DeviceTokens.SingleOrDefaultAsync(t => t.Token == token, ct);
        if (existing is null) db.DeviceTokens.Add(DeviceToken.Create(user.UserId, token, now));
        else existing.Reassign(user.UserId, now);   // device changed account

        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("me/push-tokens")]
    public async Task<IActionResult> UnregisterPushToken([FromBody] PushTokenRequest r, CancellationToken ct)
    {
        await db.DeviceTokens.Where(t => t.Token == r.Token && t.UserId == user.UserId).ExecuteDeleteAsync(ct);
        return NoContent();
    }

    // ---- In-app notification list ----

    // The user's inbox: their delivery rows joined to the shared content. The DTO keeps the user row's id
    // (that is what /read expects), so the mobile contract is unchanged.
    private IQueryable<NotificationDto> Inbox() =>
        from u in db.UserNotifications.AsNoTracking()
        join n in db.Notifications.AsNoTracking() on u.NotificationId equals n.Id
        where u.UserId == user.UserId
        orderby u.CreatedAtUtc descending
        select new NotificationDto(u.Id, n.Title, n.Body, n.DataJson, u.CreatedAtUtc, u.ReadAtUtc != null, n.ExpiresAtUtc, n.Recurring, n.Startup);

    [HttpQuery("notifications")]
    public async Task<IActionResult> List([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] MyNotificationQuery? query, CancellationToken ct)
    {
        query ??= new(null, null, null);
        var q = Inbox();
        if (query.UnreadOnly == true) q = q.Where(n => !n.IsRead);

        var rows = await q.Skip(Paging.Skip(query.Skip)).Take(Paging.Take(query.Take, 30, 100)).ToListAsync(ct);
        return Ok(rows);
    }

    [HttpGet("notifications/unread-count")]
    public async Task<IActionResult> UnreadCount(CancellationToken ct) =>
        Ok(new { count = await db.UserNotifications.CountAsync(n => n.UserId == user.UserId && n.ReadAtUtc == null, ct) });

    [HttpPost("notifications/{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        var n = await db.UserNotifications.SingleOrDefaultAsync(x => x.Id == id && x.UserId == user.UserId, ct);
        if (n is null) return Error.NotFound("notification.not_found", "Notification not found.").ToProblem();
        n.MarkRead(clock.GetUtcNow().UtcDateTime);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("notifications/read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        await db.UserNotifications.Where(n => n.UserId == user.UserId && n.ReadAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAtUtc, (DateTime?)now), ct);
        return NoContent();
    }
}
