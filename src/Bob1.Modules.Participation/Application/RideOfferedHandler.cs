using Bob1.Shared.Contracts.Carpool;
using Bob1.Shared.Contracts.Identity;
using Bob1.Shared.Contracts.Matches;
using Bob1.Shared.Contracts.Notifications;

namespace Bob1.Modules.Participation.Application;

/// <summary>ride.offered: families whose presence is confirmed learn that a carpool exists for the match (in-app).</summary>
internal sealed class RideOfferedHandler(
    ParticipationDbContext db, IIdentityApi identity, IMatchesApi matches, INotificationsApi notifications,
    ILogger<RideOfferedHandler> log)
    : IDomainEventHandler<RideOffered>
{
    public Task HandleAsync(RideOffered e, CancellationToken ct) => Safe.RunAsync(log, "ride offered", async () =>
    {
        var families = await db.PresenceConfirmations.AsNoTracking().Where(p => p.MatchId == e.MatchId)
            .Select(p => p.FamilyId).Distinct().ToListAsync(ct);
        var audience = new HashSet<Guid>();
        foreach (var familyId in families) audience.UnionWith(await identity.GetUserIdsOfFamilyAsync(familyId, ct));
        audience.Remove(e.DriverUserId);
        if (audience.Count == 0) return;

        var title = (await matches.GetAsync(e.MatchId, ct))?.Title ?? "le match";
        await notifications.SendToUsersAsync(audience, "Covoiturage proposé", $"Un covoiturage est proposé pour {title}.",
            new Dictionary<string, string> { ["type"] = "ride_offered", ["matchId"] = e.MatchId.ToString(), ["rideId"] = e.RideId.ToString() },
            ct, push: false);
    });
}
