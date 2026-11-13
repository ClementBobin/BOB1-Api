using Bob1.Shared.Contracts.Identity;
using Bob1.Shared.Contracts.Matches;
using Bob1.Shared.Contracts.Notifications;

namespace Bob1.Modules.Participation.Application;

/// <summary>
/// Matches publishes MatchCancelled; we tell everyone concerned: the players of the two teams and their families
/// (team -> user, through Identity), the families who had confirmed presence, and the staff / volunteers signed up.
/// </summary>
internal sealed class MatchCancelledHandler(ParticipationDbContext db, INotificationsApi notifications, IIdentityApi identity)
    : IDomainEventHandler<MatchCancelled>
{
    public async Task HandleAsync(MatchCancelled e, CancellationToken ct)
    {
        var recipients = new HashSet<Guid>(await identity.GetUserIdsByTeamsAsync([e.HomeTeamId, e.AwayTeamId], ct));

        // A family that confirmed may have left the team since: it still needs to know.
        var confirmedFamilies = await db.PresenceConfirmations.Where(p => p.MatchId == e.MatchId).Select(p => p.FamilyId)
            .ToListAsync(ct);
        foreach (var familyId in confirmedFamilies)
            recipients.UnionWith(await identity.GetUserIdsOfFamilyAsync(familyId, ct));

        var staffAndVolunteers = await db.Slots.Where(s => s.MatchId == e.MatchId)
            .SelectMany(s => s.Signups).Select(x => x.UserId).Distinct().ToListAsync(ct);
        recipients.UnionWith(staffAndVolunteers);

        var data = new Dictionary<string, string> { ["type"] = "match_cancelled", ["matchId"] = e.MatchId.ToString() };
        await notifications.SendToUsersAsync(recipients.ToList(), "Match annulé", $"{e.Title} a été annulé.", data, ct);
    }
}
