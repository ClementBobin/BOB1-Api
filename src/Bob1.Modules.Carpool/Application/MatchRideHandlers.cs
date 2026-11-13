using Bob1.Shared.Contracts.Club;
using Bob1.Shared.Contracts.Matches;
using Bob1.Shared.Contracts.Notifications;

namespace Bob1.Modules.Carpool.Application;

/// <summary>
/// A cancelled match leaves orphan rides: delete them and tell the drivers and the passengers. This is not a driver
/// cancellation, so RideCancelled (which costs the driver points) is deliberately not raised.
/// </summary>
internal sealed class MatchCancelledRidesHandler(CarpoolDbContext db, INotificationsApi notifications, ILogger<MatchCancelledRidesHandler> log)
    : IDomainEventHandler<MatchCancelled>
{
    public Task HandleAsync(MatchCancelled e, CancellationToken ct) => Safe.RunAsync(log, "match cancelled (rides)", async () =>
    {
        var rides = await db.Rides.Include(r => r.Bookings).Where(r => r.MatchId == e.MatchId).ToListAsync(ct);
        if (rides.Count == 0) return;

        var drivers = rides.Select(r => r.DriverUserId).Distinct().ToList();
        var passengers = rides.SelectMany(r => r.Bookings).Select(b => b.UserId).Distinct().Except(drivers).ToList();
        db.Rides.RemoveRange(rides);   // bookings cascade
        await db.SaveChangesAsync(ct);

        var data = new Dictionary<string, string> { ["type"] = "ride_cancelled", ["matchId"] = e.MatchId.ToString() };
        await notifications.SendToUsersAsync(drivers, "Covoiturage supprimé",
            $"{e.Title} est annulé : votre covoiturage a été supprimé.", data, ct);
        await notifications.SendToUsersAsync(passengers, "Covoiturage annulé",
            $"{e.Title} est annulé : le covoiturage que vous aviez réservé est supprimé.", data, ct);
    });
}

/// <summary>The date or venue of the match changed: the departure time of the rides is probably wrong.</summary>
internal sealed class MatchRescheduledRidesHandler(
    CarpoolDbContext db, INotificationsApi notifications, IClubApi club, ILogger<MatchRescheduledRidesHandler> log)
    : IDomainEventHandler<MatchRescheduled>
{
    public Task HandleAsync(MatchRescheduled e, CancellationToken ct) => Safe.RunAsync(log, "match rescheduled (rides)", async () =>
    {
        if (e.OldStartsAtUtc == e.NewStartsAtUtc && e.OldVenueId == e.NewVenueId) return;
        var rides = await db.Rides.Include(r => r.Bookings).Where(r => r.MatchId == e.MatchId).ToListAsync(ct);
        if (rides.Count == 0) return;

        var diff = await MatchDiff.DescribeAsync(club, e, ct);
        var data = new Dictionary<string, string> { ["type"] = "ride_match_rescheduled", ["matchId"] = e.MatchId.ToString() };
        var drivers = rides.Select(r => r.DriverUserId).Distinct().ToList();
        var passengers = rides.SelectMany(r => r.Bookings).Select(b => b.UserId).Distinct().Except(drivers).ToList();

        await notifications.SendToUsersAsync(drivers, "Match modifié : pensez à ajuster votre covoiturage",
            $"{e.Title} : {diff}. Mettez à jour l'heure de départ.", data, ct);
        await notifications.SendToUsersAsync(passengers, "Match modifié : votre covoiturage peut changer",
            $"{e.Title} : {diff}. Vérifiez l'heure de départ avec le conducteur.", data, ct);
    });
}
