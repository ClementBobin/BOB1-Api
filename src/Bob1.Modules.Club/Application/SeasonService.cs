namespace Bob1.Modules.Club.Application;

/// <summary>Makes sure the row of the current season exists (seasons are never created by hand).</summary>
internal static class SeasonEnsurer
{
    /// <summary>True when a NEW season row was created while an earlier one already existed (a real season change).</summary>
    public static async Task<bool> EnsureCurrentAsync(ClubDbContext db, TimeProvider clock, CancellationToken ct = default)
    {
        var label = Season.Current(clock);
        if (await db.Seasons.AnyAsync(s => s.Label == label, ct)) return false;
        var hadPrevious = await db.Seasons.AnyAsync(ct);
        db.Seasons.Add(ClubSeason.For(label));
        await db.SaveChangesAsync(ct);
        return hadPrevious;
    }
}

/// <summary>Checks hourly so the new season exists as soon as 1 September starts, without a restart.</summary>
internal sealed class SeasonService(IServiceScopeFactory scopes, TimeProvider clock, ILogger<SeasonService> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                var started = await SeasonEnsurer.EnsureCurrentAsync(scope.ServiceProvider.GetRequiredService<ClubDbContext>(), clock, stoppingToken);
                if (started)
                {
                    // New season: tell everyone (push + in-app).
                    var identity = scope.ServiceProvider.GetRequiredService<Bob1.Shared.Contracts.Identity.IIdentityApi>();
                    var notifications = scope.ServiceProvider.GetRequiredService<Bob1.Shared.Contracts.Notifications.INotificationsApi>();
                    var label = Season.Current(clock);
                    await notifications.SendToUsersAsync(await identity.GetUserIdsByDivisionAsync(null, stoppingToken),
                        "Nouvelle saison", $"La saison {label} commence. Les divisions et équipes sont reconduites : vérifiez les vôtres.",
                        new Dictionary<string, string> { ["type"] = "season_started", ["season"] = label }, stoppingToken);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogError(ex, "Season check failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
