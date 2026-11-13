using Bob1.Shared.Contracts.Identity;
using Bob1.Shared.Contracts.Matches;
using Bob1.Shared.Contracts.Notifications;

namespace Bob1.Modules.Notifications.Application;

/// <summary>
/// A match was created: tell the players of both teams (and their families). When nobody is attached to either team
/// no notification is created for them and the admins get an alert instead. Other admins get an in-app audit copy.
/// Staff are notified when a slot is opened on the match (SlotCreated), not here.
/// </summary>
internal sealed class MatchCreatedHandler(
    IIdentityApi identity, INotificationsApi notifications, ICurrentUser actor, ILogger<MatchCreatedHandler> log)
    : IDomainEventHandler<MatchCreated>
{
    public Task HandleAsync(MatchCreated e, CancellationToken ct) => Safe.RunAsync(log, "match created", async () =>
    {
        var data = new Dictionary<string, string> { ["type"] = "match_created", ["matchId"] = e.MatchId.ToString() };
        var recipients = await identity.GetUserIdsByTeamsAsync([e.HomeTeamId, e.AwayTeamId], ct);
        var when = Dates.Label(e.StartsAtUtc);
        var exceptActor = actor.IsAuthenticated ? actor.UserId : (Guid?)null;

        if (recipients.Count > 0)
            await notifications.SendToUsersAsync(recipients, "Nouveau match", $"{e.Title} le {when}.", data, ct);

        await notifications.NotifyAdminsAsync("Match créé",
            recipients.Count > 0
                ? $"{e.Title} le {when} : {recipients.Count} personne(s) prévenue(s)."
                : $"{e.Title} le {when} : aucun joueur n'est rattaché à ces équipes, personne n'a été prévenu.",
            data, exceptActor, ct);
    });
}
