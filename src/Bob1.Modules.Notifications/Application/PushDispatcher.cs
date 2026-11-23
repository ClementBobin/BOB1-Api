using System.Text.Json;

namespace Bob1.Modules.Notifications.Application;

/// <summary>Pushes a notification to the devices of its recipients (best-effort). Shared by the sender and the recurrence job.</summary>
internal sealed class PushDispatcher(NotificationsDbContext db, IPushSender push, ILogger<PushDispatcher> log)
{
    public async Task SendAsync(Notification notification, IReadOnlyCollection<Guid> userIds, CancellationToken ct)
    {
        // Push is best-effort: a Firebase outage must never break the action that triggered the notification.
        try
        {
            var tokens = await db.DeviceTokens.Where(t => userIds.Contains(t.UserId)).Select(t => t.Token).ToListAsync(ct);
            if (tokens.Count == 0) return;

            var data = notification.DataJson is null
                ? null
                : JsonSerializer.Deserialize<Dictionary<string, string>>(notification.DataJson);
            var invalid = await push.SendAsync(tokens, notification.Title, notification.Body, data, ct);
            if (invalid.Count > 0)
                await db.DeviceTokens.Where(t => invalid.Contains(t.Token)).ExecuteDeleteAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogWarning(ex, "Push delivery failed for {Count} user(s)", userIds.Count);
        }
    }
}
