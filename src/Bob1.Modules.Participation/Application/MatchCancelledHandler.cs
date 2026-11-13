using Bob1.Shared.Contracts.Identity;
using Bob1.Shared.Contracts.Matches;
using Bob1.Shared.Contracts.Notifications;

namespace Bob1.Modules.Participation.Application;

/// <summary>
/// Matches publishes MatchCancelled; we tell everyone concerned (players and their families, confirmed families,
/// people signed up, the staff when the match has staff slots) and leave an in-app audit copy to the other admins.
/// </summary>
internal sealed class MatchCancelledHandler(
    ParticipationDbContext db, INotificationsApi notifications, IIdentityApi identity, IMatchesApi matches,
    ICurrentUser actor, ILogger<MatchCancelledHandler> log)
    : IDomainEventHandler<MatchCancelled>
{
    public Task HandleAsync(MatchCancelled e, CancellationToken ct) => Safe.RunAsync(log, "match cancelled", async () =>
    {
        var audience = await MatchAudience.ResolveAsync(db, identity, matches, e.MatchId, ct);
        if (await db.Slots.AnyAsync(s => s.MatchId == e.MatchId && s.Kind == SlotKind.Staff, ct))
            audience.UnionWith(await identity.GetUserIdsByRoleAsync("Staff", ct));

        var data = new Dictionary<string, string> { ["type"] = "match_cancelled", ["matchId"] = e.MatchId.ToString() };
        const string title = "Match annulé";
        var body = $"{e.Title} ({Dates.Label(e.StartsAtUtc)}) a été annulé.";

        await notifications.SendToUsersAsync(audience, title, body, data, ct);
        await notifications.NotifyAdminsAsync(title, body + (audience.Count == 0 ? " Personne n'était concerné." : ""), data,
            actor.IsAuthenticated ? actor.UserId : null, ct);
    });
}
