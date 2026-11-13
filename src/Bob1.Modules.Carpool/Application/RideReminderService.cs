using Bob1.Shared.Contracts.Matches;
using Bob1.Shared.Contracts.Notifications;

namespace Bob1.Modules.Carpool.Application;

/// <summary>
/// Every 15 minutes, for the rides leaving in the next 24 hours:
/// - ride.reminder: the driver and the passengers get the departure time (once),
/// - ride.no_passenger: a driver nobody has booked yet is told (once).
/// De-duplicated through SendToUsersOnceAsync; the notifications expire at the departure.
/// </summary>
internal sealed class RideReminderService(IServiceScopeFactory scopes, TimeProvider clock, ILogger<RideReminderService> log)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(15));
        do
        {
            try { await RunOnceAsync(stoppingToken); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogError(ex, "Ride reminder run failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CarpoolDbContext>();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationsApi>();
        var matches = scope.ServiceProvider.GetRequiredService<IMatchesApi>();

        var now = clock.GetUtcNow().UtcDateTime;
        var rides = await db.Rides.AsNoTracking().Include(r => r.Bookings)
            .Where(r => r.DepartureUtc > now && r.DepartureUtc <= now.AddHours(24)).ToListAsync(ct);
        if (rides.Count == 0) return;

        var titles = await matches.GetManyAsync(rides.Select(r => r.MatchId), ct);
        foreach (var ride in rides)
        {
            var title = titles.TryGetValue(ride.MatchId, out var m) ? m.Title : "le match";
            Dictionary<string, string> Data(string type) =>
                new() { ["type"] = type, ["matchId"] = ride.MatchId.ToString(), ["rideId"] = ride.Id.ToString() };
            var departure = Dates.Label(ride.DepartureUtc);

            var everyone = ride.Bookings.Select(b => b.UserId).Append(ride.DriverUserId).Distinct().ToList();
            await notifications.SendToUsersOnceAsync($"ride-reminder:{ride.Id}", everyone, "Covoiturage : départ proche",
                $"{title} : départ {departure}.", Data("ride_reminder"), ct, ride.DepartureUtc);

            if (ride.Bookings.Count == 0)
                await notifications.SendToUsersOnceAsync($"ride-no-passenger:{ride.Id}", [ride.DriverUserId], "Aucun passager",
                    $"Personne n'a encore réservé votre trajet pour {title} ({departure}).", Data("ride_no_passenger"), ct,
                    ride.DepartureUtc, push: false);
        }
    }
}
