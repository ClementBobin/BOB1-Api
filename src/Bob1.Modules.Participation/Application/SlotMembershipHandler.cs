using Bob1.Shared.Contracts.Identity;
using Bob1.Shared.Contracts.Matches;
using Bob1.Shared.Contracts.Notifications;

namespace Bob1.Modules.Participation.Application;

internal sealed class SlotMembershipHandler(
    ParticipationDbContext db, IMatchesApi matches, IIdentityApi identity, ICurrentUser user, IDomainEventDispatcher events,
    INotificationsApi notifications, TimeProvider clock, ILogger<SlotMembershipHandler> log)
{
    /// <summary>Withdrawing this close to the match is the urgent case for the organisers (same window as the J-4 penalty rule).</summary>
    private const int LateWithdrawalDays = 4;

    private const int MaxAttempts = 3;

    public async Task<Result> JoinAsync(Guid slotId, CancellationToken ct)
    {
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var slot = await db.Slots.Include(s => s.Signups).SingleOrDefaultAsync(s => s.Id == slotId, ct);
            if (slot is null) return ParticipationErrors.SlotNotFound;

            var role = slot.Kind == SlotKind.Staff ? Policies.Staff : Policies.Parent;
            if (!user.IsInRole(role)) return ParticipationErrors.WrongAudience;

            var now = clock.GetUtcNow().UtcDateTime;
            var match = await matches.GetAsync(slot.MatchId, ct);
            if (MatchRules.EnsureOpen(match, now) is { } closed) return closed;
            if (await TeamRules.EnsureAllowedAsync(user, identity, match, ct) is { } notInTeam) return notInTeam;

            var joined = slot.Join(user.UserId, user.FamilyId, now);
            if (!joined.IsSuccess) return joined;

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)   // includes DbUpdateConcurrencyException: someone else took the place, re-check
            {
                db.ChangeTracker.Clear();
                continue;
            }

            await events.DispatchAsync(slot.DomainEvents, ct);
            slot.ClearDomainEvents();

            // In-app receipt for the person who signed up.
            await Safe.RunAsync(log, "slot join receipt", () => notifications.SendToPersonAsync(user.UserId, "Inscription enregistrée",
                $"Vous êtes inscrit comme {slot.Role} sur {match?.Title ?? "le match"}.",
                new Dictionary<string, string> { ["type"] = "slot_joined", ["matchId"] = slot.MatchId.ToString(), ["slotId"] = slot.Id.ToString() },
                ct, push: false));
            return Result.Success();
        }
        return Error.Conflict("slot.contention", "Too many simultaneous requests, please retry.");
    }

    public async Task<Result> LeaveAsync(Guid slotId, CancellationToken ct)
    {
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var slot = await db.Slots.Include(s => s.Signups).SingleOrDefaultAsync(s => s.Id == slotId, ct);
            if (slot is null) return ParticipationErrors.SlotNotFound;

            var now = clock.GetUtcNow().UtcDateTime;
            var match = await matches.GetAsync(slot.MatchId, ct);
            if (match is not null && match.StartsAtUtc <= now) return ParticipationErrors.MatchAlreadyStarted;

            var signup = slot.Signups.SingleOrDefault(s => s.UserId == user.UserId);
            var left = slot.Leave(user.UserId);
            if (!left.IsSuccess) return left;
            if (signup is not null) db.SlotSignups.Remove(signup);

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                db.ChangeTracker.Clear();
                continue;
            }

            await events.DispatchAsync(slot.DomainEvents, ct);
            slot.ClearDomainEvents();

            if (match is not null && match.StartsAtUtc - now <= TimeSpan.FromDays(LateWithdrawalDays))
                await Safe.RunAsync(log, "late withdrawal", async () =>
                {
                    var name = (await identity.GetUserNamesAsync([user.UserId], ct)).GetValueOrDefault(user.UserId, "Quelqu'un");
                    var admins = (await identity.GetUserIdsByRoleAsync("Admin", ct)).Where(id => id != user.UserId).ToList();
                    await notifications.SendToUsersAsync(admins, "Désinscription tardive",
                        $"{name} s'est désinscrit du poste {slot.Role} sur {match.Title} ({Dates.Label(match.StartsAtUtc)}).",
                        new Dictionary<string, string> { ["type"] = "slot_late_withdrawal", ["matchId"] = slot.MatchId.ToString(), ["slotId"] = slot.Id.ToString() }, ct);
                });
            return Result.Success();
        }
        return Error.Conflict("slot.contention", "Too many simultaneous requests, please retry.");
    }
}
