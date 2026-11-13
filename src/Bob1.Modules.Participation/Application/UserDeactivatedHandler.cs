using Bob1.Shared.Contracts.Identity;
using Bob1.Shared.Contracts.Matches;
using Bob1.Shared.Contracts.Notifications;

namespace Bob1.Modules.Participation.Application;

/// <summary>A disabled account stays signed up to slots: admins are told which holes to fill (push).</summary>
internal sealed class UserDeactivatedHandler(
    ParticipationDbContext db, IMatchesApi matches, IIdentityApi identity, INotificationsApi notifications,
    TimeProvider clock, ILogger<UserDeactivatedHandler> log)
    : IDomainEventHandler<UserDeactivated>
{
    public Task HandleAsync(UserDeactivated e, CancellationToken ct) => Safe.RunAsync(log, "user deactivated (slots)", async () =>
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var matchIds = await db.SlotSignups.AsNoTracking().Where(x => x.UserId == e.UserId)
            .Join(db.Slots, x => x.SlotId, s => s.Id, (_, s) => s.MatchId).Distinct().ToListAsync(ct);
        var upcoming = (await matches.GetManyAsync(matchIds, ct)).Values
            .Where(m => m.StartsAtUtc > now && m.Status == MatchStatus.Scheduled).OrderBy(m => m.StartsAtUtc).ToList();
        if (upcoming.Count == 0) return;

        var name = (await identity.GetUserNamesAsync([e.UserId], ct)).GetValueOrDefault(e.UserId, "Une personne");
        var admins = (await identity.GetUserIdsByRoleAsync("Admin", ct)).Where(id => id != e.UserId);
        await notifications.SendToUsersAsync(admins, "Compte désactivé : postes à pourvoir",
            $"{name} est inscrit(e) sur {upcoming.Count} match(s) à venir (le prochain : {upcoming[0].Title}, {Dates.Label(upcoming[0].StartsAtUtc)}).",
            new Dictionary<string, string> { ["type"] = "user_deactivated_slots", ["personId"] = e.UserId.ToString() }, ct);
    });
}
