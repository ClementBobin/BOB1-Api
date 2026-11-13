using System.Text.Json;
using Bob1.Shared.Contracts.Identity;
using Bob1.Shared.Contracts.Notifications;

namespace Bob1.Modules.Notifications.Application;

internal sealed class NotificationsApi(
    NotificationsDbContext db, IIdentityApi identity, IPushSender push, TimeProvider clock, ILogger<NotificationsApi> log)
    : INotificationsApi
{
    public async Task SendToPersonAsync(Guid personId, string title, string body,
        IReadOnlyDictionary<string, string>? data = null, CancellationToken ct = default,
        DateTime? expiresAtUtc = null, bool recurring = false, bool startup = false, bool push = true)
    {
        await SendToUsersAsync([personId], title, body, data, ct, expiresAtUtc, recurring, startup, push);
    }

    public async Task<int> SendToUsersOnceAsync(string dedupeKey, IEnumerable<Guid> userIds, string title, string body,
        IReadOnlyDictionary<string, string>? data = null, CancellationToken ct = default,
        DateTime? expiresAtUtc = null, bool push = true)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0) return 0;

        // The key travels in the notification's data, so "already sent" is a lookup there (no extra table).
        var payload = new Dictionary<string, string>(data ?? new Dictionary<string, string>()) { ["dedupe"] = dedupeKey };
        var marker = $"\"dedupe\":{JsonSerializer.Serialize(dedupeKey)}";
        var already = await db.Notifications.Where(n => ids.Contains(n.UserId) && n.DataJson != null && n.DataJson.Contains(marker))
            .Select(n => n.UserId).Distinct().ToListAsync(ct);
        var targets = ids.Except(already).ToList();
        if (targets.Count == 0) return 0;

        await SendToUsersAsync(targets, title, body, payload, ct, expiresAtUtc, push: push);
        return targets.Count;
    }

    public async Task NotifyAdminsAsync(string title, string body, IReadOnlyDictionary<string, string>? data = null,
        Guid? exceptUserId = null, CancellationToken ct = default)
    {
        var admins = (await identity.GetUserIdsByRoleAsync("Admin", ct)).Where(id => id != exceptUserId).ToList();
        await SendToUsersAsync(admins, title, body, data, ct, push: false);
    }

    public async Task SendToUsersAsync(IEnumerable<Guid> userIds, string title, string body,
        IReadOnlyDictionary<string, string>? data = null, CancellationToken ct = default,
        DateTime? expiresAtUtc = null, bool recurring = false, bool startup = false, bool push = true)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0) return;

        var now = clock.GetUtcNow().UtcDateTime;
        var dataJson = data is null ? null : JsonSerializer.Serialize(data);
        db.Notifications.AddRange(ids.Select(id => UserNotification.Create(id, title, body, dataJson, now, expiresAtUtc, recurring, startup)));
        await db.SaveChangesAsync(ct);

        if (!push) return;

        // Push is best-effort: a Firebase outage must never break the action that triggered the notification.
        try
        {
            var tokens = await db.DeviceTokens.Where(t => ids.Contains(t.UserId)).Select(t => t.Token).ToListAsync(ct);
            if (tokens.Count == 0) return;

            var invalid = await push.SendAsync(tokens, title, body, data, ct);
            if (invalid.Count > 0)
                await db.DeviceTokens.Where(t => invalid.Contains(t.Token)).ExecuteDeleteAsync(ct);
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Push delivery failed for {Count} user(s)", ids.Count);
        }
    }
}
