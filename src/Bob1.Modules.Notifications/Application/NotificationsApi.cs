using System.Text.Json;
using Bob1.Shared.Contracts.Identity;
using Bob1.Shared.Contracts.Notifications;

namespace Bob1.Modules.Notifications.Application;

internal sealed class NotificationsApi(
    NotificationsDbContext db, IIdentityApi identity, IPushSender push, ICurrentUser actor, TimeProvider clock,
    ILogger<NotificationsApi> log)
    : INotificationsApi
{
    public async Task SendToPersonAsync(Guid personId, string title, string body,
        IReadOnlyDictionary<string, string>? data = null, CancellationToken ct = default,
        DateTime? expiresAtUtc = null, bool recurring = false, bool startup = false, bool push = true)
    {
        // Personal notices (penalty, bonus, role change...) are for the recipient only: hidden from the admin history.
        await SendToUsersAsync([personId], title, body, data, ct, expiresAtUtc, recurring, startup, push, adminVisible: false);
    }

    public async Task<int> SendToUsersOnceAsync(string dedupeKey, IEnumerable<Guid> userIds, string title, string body,
        IReadOnlyDictionary<string, string>? data = null, CancellationToken ct = default,
        DateTime? expiresAtUtc = null, bool push = true)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0) return 0;

        var now = clock.GetUtcNow().UtcDateTime;
        var notification = await db.Notifications.SingleOrDefaultAsync(n => n.DedupeKey == dedupeKey, ct);
        if (notification is null)
        {
            notification = Build(title, body, data, now, adminVisible: false, expiresAtUtc, recurring: false, startup: false, dedupeKey);
            db.Notifications.Add(notification);
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException)
            {
                // Another worker created the same key first (unique index): attach to theirs.
                db.Entry(notification).State = EntityState.Detached;
                notification = await db.Notifications.SingleAsync(n => n.DedupeKey == dedupeKey, ct);
            }
        }

        var already = await db.UserNotifications.Where(u => u.NotificationId == notification.Id && ids.Contains(u.UserId))
            .Select(u => u.UserId).ToListAsync(ct);
        var targets = ids.Except(already).ToList();
        if (targets.Count == 0) return 0;

        await DeliverAsync(notification, targets, now, push, ct);
        return targets.Count;
    }

    public async Task NotifyAdminsAsync(string title, string body, IReadOnlyDictionary<string, string>? data = null,
        Guid? exceptUserId = null, CancellationToken ct = default)
    {
        var admins = (await identity.GetUserIdsByRoleAsync("Admin", ct)).Where(id => id != exceptUserId).ToList();
        // Audit copy: in-app only, and not part of the admin history (it would duplicate the real notification).
        await SendToUsersAsync(admins, title, body, data, ct, push: false, adminVisible: false);
    }

    public async Task SendToUsersAsync(IEnumerable<Guid> userIds, string title, string body,
        IReadOnlyDictionary<string, string>? data = null, CancellationToken ct = default,
        DateTime? expiresAtUtc = null, bool recurring = false, bool startup = false, bool push = true,
        bool adminVisible = true)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0) return;

        var now = clock.GetUtcNow().UtcDateTime;
        var notification = Build(title, body, data, now, adminVisible, expiresAtUtc, recurring, startup, dedupeKey: null);
        db.Notifications.Add(notification);
        await db.SaveChangesAsync(ct);

        await DeliverAsync(notification, ids, now, push, ct);
    }

    private Notification Build(string title, string body, IReadOnlyDictionary<string, string>? data, DateTime now,
        bool adminVisible, DateTime? expiresAtUtc, bool recurring, bool startup, string? dedupeKey)
    {
        var type = data is not null && data.TryGetValue("type", out var t) && !string.IsNullOrWhiteSpace(t) ? t : "broadcast";
        var payload = data is null ? null : new Dictionary<string, string>(data);
        if (dedupeKey is not null) (payload ??= new())["dedupe"] = dedupeKey;
        return Notification.Create(type, title, body, payload is null ? null : JsonSerializer.Serialize(payload), now,
            adminVisible, actor.IsAuthenticated ? actor.UserId : null, dedupeKey, expiresAtUtc, recurring, startup);
    }

    /// <summary>Writes the per-user rows, then pushes to their devices (best-effort).</summary>
    private async Task DeliverAsync(Notification notification, IReadOnlyCollection<Guid> userIds, DateTime now, bool pushEnabled, CancellationToken ct)
    {
        db.UserNotifications.AddRange(userIds.Select(id => UserNotification.Create(notification.Id, id, now)));
        await db.SaveChangesAsync(ct);

        if (!pushEnabled) return;

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
        catch (Exception ex)
        {
            log.LogWarning(ex, "Push delivery failed for {Count} user(s)", userIds.Count);
        }
    }
}
