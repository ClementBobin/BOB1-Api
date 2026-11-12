namespace Bob1.Modules.Identity.Application;

/// <summary>
/// Season memberships. The current season is derived from today's date; the first time we are asked in a new
/// season, the previous season's memberships are copied (players "stay" by default). Edits only touch the current season.
/// </summary>
internal sealed class MembershipService(IdentityDbContext db, TimeProvider clock)
{
    private static string? _ensuredFor;                       // season already prepared by this process
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public string CurrentSeason => Season.Current(clock);

    /// <summary>Returns the current season, after making sure its memberships exist (idempotent, cheap once done).</summary>
    public async Task<string> EnsureCurrentSeasonAsync(CancellationToken ct = default)
    {
        var current = CurrentSeason;
        if (_ensuredFor == current) return current;

        await Gate.WaitAsync(ct);
        try
        {
            if (_ensuredFor == current) return current;
            if (!await db.Memberships.AnyAsync(m => m.Season == current, ct))
            {
                var source = await db.Memberships.Where(m => string.Compare(m.Season, current) < 0)
                    .OrderByDescending(m => m.Season).Select(m => m.Season).FirstOrDefaultAsync(ct);
                if (source is not null)
                {
                    var now = clock.GetUtcNow().UtcDateTime;
                    var rows = await db.Memberships.AsNoTracking()
                        .Where(m => m.Season == source && db.Users.Any(u => u.Id == m.UserId && u.IsActive))
                        .ToListAsync(ct);
                    db.Memberships.AddRange(rows.Select(m => Membership.Create(m.UserId, current, m.DivisionId, now)));
                    var teamRows = await db.TeamMemberships.AsNoTracking()
                        .Where(t => t.Season == source && db.Users.Any(u => u.Id == t.UserId && u.IsActive))
                        .ToListAsync(ct);
                    db.TeamMemberships.AddRange(teamRows.Select(t => TeamMembership.Create(t.UserId, current, t.TeamId)));
                    await db.SaveChangesAsync(ct);
                }
            }
            _ensuredFor = current;
            return current;
        }
        finally { Gate.Release(); }
    }

    /// <summary>Sets (or clears, with null) the division of a user for the CURRENT season only.</summary>
    public async Task SetCurrentAsync(Guid userId, Guid? divisionId, CancellationToken ct)
    {
        var season = await EnsureCurrentSeasonAsync(ct);
        var row = await db.Memberships.SingleOrDefaultAsync(m => m.UserId == userId && m.Season == season, ct);
        if (divisionId is null)
        {
            if (row is not null) db.Memberships.Remove(row);
        }
        else if (row is null)
            db.Memberships.Add(Membership.Create(userId, season, divisionId.Value, clock.GetUtcNow().UtcDateTime));
        else
            row.MoveTo(divisionId.Value);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Replaces the teams of a user for the CURRENT season only (history is kept).</summary>
    public async Task SetCurrentTeamsAsync(Guid userId, IEnumerable<Guid> teamIds, CancellationToken ct)
    {
        var season = await EnsureCurrentSeasonAsync(ct);
        var wanted = teamIds.Distinct().ToHashSet();
        var existing = await db.TeamMemberships.Where(t => t.UserId == userId && t.Season == season).ToListAsync(ct);
        db.TeamMemberships.RemoveRange(existing.Where(t => !wanted.Contains(t.TeamId)));
        foreach (var id in wanted.Where(id => existing.All(t => t.TeamId != id)))
            db.TeamMemberships.Add(TeamMembership.Create(userId, season, id));
        await db.SaveChangesAsync(ct);
    }

    /// <summary>User → teams for a season.</summary>
    public async Task<Dictionary<Guid, List<Guid>>> TeamsAsync(string season, IEnumerable<Guid> userIds, CancellationToken ct)
    {
        var ids = userIds.Distinct().ToList();
        var rows = await db.TeamMemberships.AsNoTracking().Where(t => t.Season == season && ids.Contains(t.UserId)).ToListAsync(ct);
        return rows.GroupBy(t => t.UserId).ToDictionary(g => g.Key, g => g.Select(t => t.TeamId).ToList());
    }

    /// <summary>User → division for a season (users without a membership are absent).</summary>
    public async Task<Dictionary<Guid, Guid>> DivisionsAsync(string season, IEnumerable<Guid> userIds, CancellationToken ct)
    {
        var ids = userIds.Distinct().ToList();
        return await db.Memberships.AsNoTracking().Where(m => m.Season == season && ids.Contains(m.UserId))
            .ToDictionaryAsync(m => m.UserId, m => m.DivisionId, ct);
    }
}

internal sealed class MembershipRolloverService(IServiceScopeFactory scopes, ILogger<MembershipRolloverService> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<MembershipService>().EnsureCurrentSeasonAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogError(ex, "Season membership rollover failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
