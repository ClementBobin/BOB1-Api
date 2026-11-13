using Bob1.Shared.Contracts.Identity;
using Bob1.Shared.Contracts.Matches;
using Bob1.Shared.Contracts.Notifications;
using Bob1.Shared.Contracts.Ranking;

namespace Bob1.Modules.Participation.Application;

/// <summary>
/// Every 15 minutes, for the matches of the next 15 days:
/// - slot.urgent_unfilled: open slots at J-4 and J-1 (staff / parents of the teams / admins),
/// - match.reminder: J-1 reminder for the people signed up and the confirmed families,
/// - penalty.deadline_reminder: what a late withdrawal costs at J-15 and J-4.
/// Each reminder goes out once per (type, entity, user) through SendToUsersOnceAsync and expires when the match starts.
/// </summary>
internal sealed class ParticipationReminderService(IServiceScopeFactory scopes, TimeProvider clock, ILogger<ParticipationReminderService> log)
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
                log.LogError(ex, "Participation reminder run failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<ParticipationDbContext>();
        var matches = sp.GetRequiredService<IMatchesApi>();
        var notifications = sp.GetRequiredService<INotificationsApi>();
        var identity = sp.GetRequiredService<IIdentityApi>();
        var ranking = sp.GetRequiredService<IRankingApi>();

        var now = clock.GetUtcNow().UtcDateTime;
        var upcoming = await matches.GetStartingBetweenAsync(now, now.AddDays(15), ct);
        if (upcoming.Count == 0) return;

        var staff = await identity.GetUserIdsByRoleAsync("Staff", ct);
        var parents = (await identity.GetUserIdsByRoleAsync("Parent", ct)).ToHashSet();
        var admins = await identity.GetUserIdsByRoleAsync("Admin", ct);
        var rule = await ranking.GetLateCancelRuleAsync(ct);

        foreach (var match in upcoming)
        {
            var days = (match.StartsAtUtc - now).TotalDays;
            var data = (string type) => new Dictionary<string, string> { ["type"] = type, ["matchId"] = match.Id.ToString() };
            var when = Dates.Label(match.StartsAtUtc);
            var slots = await db.Slots.AsNoTracking().Include(s => s.Signups).Where(s => s.MatchId == match.Id).ToListAsync(ct);

            // --- slot.urgent_unfilled (J-4, then J-1) ---
            if (days <= 4)
            {
                var window = days <= 1 ? "j1" : "j4";
                IReadOnlyList<Guid>? volunteers = null;
                foreach (var slot in slots.Where(s => s.Filled < s.Capacity))
                {
                    var audience = slot.Kind == SlotKind.Staff
                        ? staff
                        : volunteers ??= (await identity.GetUserIdsByTeamsAsync([match.HomeTeamId, match.AwayTeamId], ct))
                            .Where(parents.Contains).ToList();
                    var missing = slot.Capacity - slot.Filled;
                    var body = $"Il manque {missing} personne(s) pour « {slot.Role} » sur {match.Title} ({when}).";
                    var d = data("slot_urgent_unfilled");
                    d["slotId"] = slot.Id.ToString();
                    await notifications.SendToUsersOnceAsync($"slot-urgent-{window}:{slot.Id}", audience.Concat(admins).Distinct(),
                        "Poste urgent à pourvoir", body, d, ct, match.StartsAtUtc);
                }
            }

            var signedUp = slots.SelectMany(s => s.Signups).Select(x => x.UserId).Distinct().ToList();

            // --- match.reminder (J-1): signed up people + confirmed families ---
            if (days <= 1)
            {
                var families = await db.PresenceConfirmations.AsNoTracking().Where(p => p.MatchId == match.Id)
                    .Select(p => p.FamilyId).Distinct().ToListAsync(ct);
                var audience = new HashSet<Guid>(signedUp);
                foreach (var familyId in families) audience.UnionWith(await identity.GetUserIdsOfFamilyAsync(familyId, ct));
                await notifications.SendToUsersOnceAsync($"match-j1:{match.Id}", audience, "Match demain",
                    $"Rappel : {match.Title}, {when}.", data("match_reminder"), ct, match.StartsAtUtc);
            }

            // --- penalty.deadline_reminder: J-15 then J-4, for the people signed up to a slot ---
            if (signedUp.Count > 0)
            {
                if (days <= 4 && rule.PointsJ4 is { } late)
                    await notifications.SendToUsersOnceAsync($"penalty-j4:{match.Id}", signedUp, "Désistement : dernier délai",
                        $"{match.Title} : se désinscrire maintenant coûte {late} pts.", data("penalty_deadline"), ct, match.StartsAtUtc);
                else if (days is > 4 and <= 15 && rule.PointsJ15 is { } soon)
                    await notifications.SendToUsersOnceAsync($"penalty-j15:{match.Id}", signedUp, "Désistement : pensez-y",
                        $"{match.Title} : vous pouvez vous désinscrire sans pénalité jusqu'à J-15 ; ensuite, un désistement coûte {soon} pts.",
                        data("penalty_deadline"), ct, match.StartsAtUtc);
            }
        }
    }
}
