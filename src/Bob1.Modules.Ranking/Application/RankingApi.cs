using Bob1.Shared.Contracts.Ranking;

namespace Bob1.Modules.Ranking.Application;

internal sealed class RankingApi(RankingDbContext db) : IRankingApi
{
    public async Task<RankingSeasonTotals> GetSeasonTotalsAsync(string season, CancellationToken ct)
    {
        var q = db.PointEntries.AsNoTracking().Where(p => p.Season == season);
        return new RankingSeasonTotals(
            await q.CountAsync(ct),
            await q.Where(p => p.Points > 0).SumAsync(p => p.Points, ct),
            await q.Where(p => p.Points < 0).SumAsync(p => p.Points, ct),
            await q.CountAsync(p => p.SourceKey.StartsWith(PointEntry.ManualPrefix), ct));
    }

    public async Task<LateCancelRule> GetLateCancelRuleAsync(CancellationToken ct)
    {
        var rule = await db.Rules.AsNoTracking().SingleOrDefaultAsync(r => r.Key == RuleKeys.PenaltyLateCancel, ct);
        return rule is null ? new LateCancelRule(0, null, null) : new LateCancelRule(rule.Points, rule.PointsJ15, rule.PointsJ4);
    }
}
