using Bob1.Shared.Contracts.Club;
using Bob1.Shared.Contracts.Matches;

namespace Bob1.Modules.Matches.Application;

internal sealed class MatchesApi(MatchesDbContext db, IClubApi club) : IMatchesApi
{
    private static MatchInfo Map(Match m, MatchLabels l) =>
        new(m.Id, l.Title(m), l.Venue(m), m.StartsAtUtc, m.Status, m.HomeTeamId, m.AwayTeamId);

    private async Task<List<MatchInfo>> MapAsync(IReadOnlyCollection<Match> rows, CancellationToken ct)
    {
        var labels = await MatchLabels.LoadAsync(club, rows, ct);
        return rows.Select(m => Map(m, labels)).ToList();
    }

    public async Task<MatchInfo?> GetAsync(Guid matchId, CancellationToken ct)
    {
        var m = await db.Matches.AsNoTracking().SingleOrDefaultAsync(x => x.Id == matchId, ct);
        return m is null ? null : (await MapAsync([m], ct))[0];
    }

    public async Task<IReadOnlyDictionary<Guid, MatchInfo>> GetManyAsync(IEnumerable<Guid> matchIds, CancellationToken ct)
    {
        var ids = matchIds.Distinct().ToList();
        var rows = await db.Matches.AsNoTracking().Where(x => ids.Contains(x.Id)).ToListAsync(ct);
        return (await MapAsync(rows, ct)).ToDictionary(m => m.Id);
    }

    public async Task<IReadOnlyList<MatchInfo>> GetStartingBetweenAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct)
    {
        var rows = await db.Matches.AsNoTracking()
            .Where(m => m.StartsAtUtc >= fromUtc && m.StartsAtUtc <= toUtc && m.Status == MatchStatus.Scheduled)
            .OrderBy(m => m.StartsAtUtc).ToListAsync(ct);
        return await MapAsync(rows, ct);
    }

    public async Task<MatchSeasonStats> GetSeasonStatsAsync(string season, CancellationToken ct)
    {
        var counts = await db.Matches.AsNoTracking().Where(m => m.Season == season)
            .GroupBy(m => m.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        int Of(MatchStatus s) => counts.Where(c => c.Key == s).Select(c => c.Count).FirstOrDefault();
        return new MatchSeasonStats(counts.Sum(c => c.Count), Of(MatchStatus.Played), Of(MatchStatus.Cancelled));
    }
}
