using Bob1.Shared.Contracts.Privacy;

namespace Bob1.Modules.Club.Application;

internal sealed class ClubPersonalData(ClubDbContext db) : IPersonalDataContributor
{
    public string Section => "club";

    public async Task<object> ExportAsync(PersonalDataSubject s, CancellationToken ct)
    {
        var coachOf = await db.Teams.AsNoTracking().Where(t => t.CoachUserId == s.UserId).Select(t => new { t.Name }).ToListAsync(ct);
        return new { coachOfTeams = coachOf };
    }

    public async Task EraseAsync(PersonalDataSubject s, CancellationToken ct) =>
        await db.Teams.Where(t => t.CoachUserId == s.UserId)
            .ExecuteUpdateAsync(u => u.SetProperty(t => t.CoachUserId, (Guid?)null).SetProperty(t => t.CoachName, (string?)null), ct);
}
