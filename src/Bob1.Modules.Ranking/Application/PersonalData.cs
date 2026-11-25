using Bob1.Shared.Contracts.Privacy;

namespace Bob1.Modules.Ranking.Application;

internal sealed class RankingPersonalData(RankingDbContext db) : IPersonalDataContributor
{
    public string Section => "ranking";

    public async Task<object> ExportAsync(PersonalDataSubject s, CancellationToken ct)
    {
        var points = await db.PointEntries.AsNoTracking().Where(p => p.PersonId == s.UserId).OrderBy(p => p.CreatedAtUtc)
            .Select(p => new { p.Season, p.Category, p.Points, p.Reason, p.MatchId, p.CreatedAtUtc }).ToListAsync(ct);
        return new { points };
    }

    public async Task EraseAsync(PersonalDataSubject s, CancellationToken ct)
    {
        await db.PointEntries.Where(p => p.PersonId == s.UserId).ExecuteDeleteAsync(ct);
        // Penalties / bonuses this person (an admin) gave to others stay, without saying who.
        await db.PointEntries.Where(p => p.ActorUserId == s.UserId)
            .ExecuteUpdateAsync(u => u.SetProperty(p => p.ActorUserId, (Guid?)null), ct);
    }
}
