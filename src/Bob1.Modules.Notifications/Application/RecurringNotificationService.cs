namespace Bob1.Modules.Notifications.Application;

/// <summary>
/// Every 5 minutes: a recurring notification (Recurring + RecurrenceHours + a future ExpiresAtUtc) that is due again is
/// re-delivered to the people who already received it: their copy becomes unread and moves back to the top of their inbox,
/// and a push goes out. No new rows are created, so the admin history keeps one entry. Stops by itself at the expiry date.
/// </summary>
internal sealed class RecurringNotificationService(IServiceScopeFactory scopes, TimeProvider clock, ILogger<RecurringNotificationService> log)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        do
        {
            try { await RunOnceAsync(stoppingToken); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogError(ex, "Recurring notification run failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    internal async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        var pusher = scope.ServiceProvider.GetRequiredService<PushDispatcher>();

        var now = clock.GetUtcNow().UtcDateTime;
        // Few rows (only recurring ones that have not expired): the "is it due" arithmetic is done in memory.
        var candidates = await db.Notifications.AsNoTracking()
            .Where(n => n.Recurring && n.RecurrenceHours != null && n.ExpiresAtUtc > now).ToListAsync(ct);

        foreach (var n in candidates.Where(n => n.IsDueForResend(now)))
        {
            // Claim this occurrence (compare-and-set on LastSentAtUtc) so two API instances never send it twice.
            var previous = n.LastSentAtUtc;
            var claimed = await db.Notifications.Where(x => x.Id == n.Id && x.LastSentAtUtc == previous)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.LastSentAtUtc, (DateTime?)now), ct);
            if (claimed == 0) continue;

            await db.UserNotifications.Where(u => u.NotificationId == n.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.ReadAtUtc, (DateTime?)null).SetProperty(u => u.CreatedAtUtc, now), ct);

            var userIds = await db.UserNotifications.AsNoTracking().Where(u => u.NotificationId == n.Id)
                .Select(u => u.UserId).ToListAsync(ct);
            await pusher.SendAsync(n, userIds, ct);
            log.LogInformation("Recurring notification {Id} re-sent to {Count} user(s)", n.Id, userIds.Count);
        }
    }
}
