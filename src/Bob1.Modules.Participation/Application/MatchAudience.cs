using Bob1.Shared.Contracts.Identity;
using Bob1.Shared.Contracts.Matches;

namespace Bob1.Modules.Participation.Application;

/// <summary>Everyone concerned by a match: confirmed families, people signed up to a slot, and the players of both teams.</summary>
internal static class MatchAudience
{
    public static async Task<HashSet<Guid>> ResolveAsync(
        ParticipationDbContext db, IIdentityApi identity, IMatchesApi matches, Guid matchId, CancellationToken ct)
    {
        var users = new HashSet<Guid>();

        var families = await db.PresenceConfirmations.Where(p => p.MatchId == matchId).Select(p => p.FamilyId)
            .Distinct().ToListAsync(ct);
        foreach (var familyId in families)
            users.UnionWith(await identity.GetUserIdsOfFamilyAsync(familyId, ct));

        users.UnionWith(await db.Slots.Where(s => s.MatchId == matchId)
            .SelectMany(s => s.Signups).Select(x => x.UserId).Distinct().ToListAsync(ct));

        if (await matches.GetAsync(matchId, ct) is { } match)
            users.UnionWith(await identity.GetUserIdsByTeamsAsync([match.HomeTeamId, match.AwayTeamId], ct));
        return users;
    }
}
