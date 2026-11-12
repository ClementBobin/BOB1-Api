namespace Bob1.Modules.Club.Application;

/// <summary>Makes sure the row of the current season exists (seasons are never created by hand).</summary>
internal static class SeasonEnsurer
{
    public static async Task EnsureCurrentAsync(ClubDbContext db, TimeProvider clock, CancellationToken ct = default)
    {
        var label = Season.Current(clock);
        if (await db.Seasons.AnyAsync(s => s.Label == label, ct)) return;
        db.Seasons.Add(ClubSeason.For(label));
        await db.SaveChangesAsync(ct);
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
                await SeasonEnsurer.EnsureCurrentAsync(scope.ServiceProvider.GetRequiredService<ClubDbContext>(), clock, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogError(ex, "Season check failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
