using Bob1.Shared.Contracts.Club;
using Bob1.Shared.Contracts.Identity;
using Bob1.Shared.Contracts.Matches;
using Bob1.Shared.Contracts.Notifications;

namespace Bob1.Modules.Participation.Application;

/// <summary>Date, venue or team of a match changed: warn everyone concerned with an "old -> new" diff.</summary>
internal sealed class MatchRescheduledHandler(
    ParticipationDbContext db, INotificationsApi notifications, IIdentityApi identity, IMatchesApi matches, IClubApi club,
    ILogger<MatchRescheduledHandler> log)
    : IDomainEventHandler<MatchRescheduled>
{
    public Task HandleAsync(MatchRescheduled e, CancellationToken ct) => Safe.RunAsync(log, "match rescheduled", async () =>
    {
        var audience = await MatchAudience.ResolveAsync(db, identity, matches, e.MatchId, ct);
        // The players of the previous teams are concerned too when a team changed.
        if (e.OldHomeTeamId != e.NewHomeTeamId || e.OldAwayTeamId != e.NewAwayTeamId)
            audience.UnionWith(await identity.GetUserIdsByTeamsAsync([e.OldHomeTeamId, e.OldAwayTeamId], ct));

        var diff = await MatchDiff.DescribeAsync(club, e, ct);
        await notifications.SendToUsersAsync(audience, "Match modifié", $"{e.Title} : {diff}",
            new Dictionary<string, string> { ["type"] = "match_rescheduled", ["matchId"] = e.MatchId.ToString() }, ct);
    });
}
