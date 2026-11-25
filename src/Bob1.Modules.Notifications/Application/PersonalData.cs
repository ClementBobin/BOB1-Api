using Bob1.Shared.Contracts.Privacy;

namespace Bob1.Modules.Notifications.Application;

internal sealed class NotificationsPersonalData(NotificationsDbContext db) : IPersonalDataContributor
{
    public string Section => "notifications";

    public async Task<object> ExportAsync(PersonalDataSubject s, CancellationToken ct)
    {
        var received = await db.UserNotifications.AsNoTracking().Where(u => u.UserId == s.UserId)
            .Join(db.Notifications, u => u.NotificationId, n => n.Id,
                (u, n) => new { n.Type, n.Title, n.Body, ReceivedAtUtc = u.CreatedAtUtc, u.ReadAtUtc })
            .OrderBy(x => x.ReceivedAtUtc).ToListAsync(ct);
        // The push token itself is a technical credential and is not part of the export: only that a device is registered.
        var devices = await db.DeviceTokens.AsNoTracking().Where(d => d.UserId == s.UserId)
            .Select(d => new { d.LastSeenAtUtc }).ToListAsync(ct);
        return new { received, pushDevices = devices };
    }

    public async Task EraseAsync(PersonalDataSubject s, CancellationToken ct)
    {
        var id = s.UserId.ToString();
        var receivedIds = await db.UserNotifications.Where(u => u.UserId == s.UserId).Select(u => u.NotificationId).ToListAsync(ct);

        await db.DeviceTokens.Where(d => d.UserId == s.UserId).ExecuteDeleteAsync(ct);
        await db.UserNotifications.Where(u => u.UserId == s.UserId).ExecuteDeleteAsync(ct);
        // Personal notices (penalty, bonus, reminders) that nobody else received have no reason to stay.
        await db.Notifications.Where(n => receivedIds.Contains(n.Id) && !n.VisibleInAdmin
                && !db.UserNotifications.Any(u => u.NotificationId == n.Id)).ExecuteDeleteAsync(ct);
        await db.Notifications.Where(n => n.CreatedByUserId == s.UserId)
            .ExecuteUpdateAsync(u => u.SetProperty(n => n.CreatedByUserId, (Guid?)null), ct);
        // Notices sent to others about this person (e.g. "X is signed up to N matches"): the title stays, the content that names them goes.
        await db.Notifications.Where(n => n.DataJson != null && n.DataJson.Contains(id))
            .ExecuteUpdateAsync(u => u
                .SetProperty(n => n.Body, "Contenu supprimé à la demande de la personne concernée.")
                .SetProperty(n => n.DataJson, (string?)null), ct);
    }
}
