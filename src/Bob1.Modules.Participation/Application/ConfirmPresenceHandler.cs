using Bob1.Shared.Contracts.Identity;
using Bob1.Shared.Contracts.Matches;
using Bob1.Shared.Contracts.Notifications;
using Bob1.Shared.Contracts.Participation;

namespace Bob1.Modules.Participation.Application;

internal sealed record ConfirmPresenceCommand(Guid MatchId);

internal sealed class ConfirmPresenceHandler(
    ParticipationDbContext db, IMatchesApi matches, IIdentityApi identity, ICurrentUser user, IDomainEventDispatcher events,
    INotificationsApi notifications, TimeProvider clock, ILogger<ConfirmPresenceHandler> log)
{
    public async Task<Result> HandleAsync(ConfirmPresenceCommand command, CancellationToken ct)
    {
        if (user.FamilyId is not { } familyId)
            return ParticipationErrors.NoFamily;

        var now = clock.GetUtcNow().UtcDateTime;
        var match = await matches.GetAsync(command.MatchId, ct);
        if (MatchRules.EnsureOpen(match, now) is { } closed)
            return closed;
        if (await TeamRules.EnsureAllowedAsync(user, identity, match, ct) is { } notInTeam)
            return notInTeam;

        // Idempotent: confirming twice is a success, not an error.
        if (await db.PresenceConfirmations.AnyAsync(p => p.MatchId == command.MatchId && p.FamilyId == familyId, ct))
            return Result.Success();

        var confirmation = PresenceConfirmation.Create(command.MatchId, familyId, user.UserId, now);
        db.PresenceConfirmations.Add(confirmation);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation()) // UNIQUE / PRIMARY KEY
        {
            return Result.Success(); // two parents confirmed at the same instant: the unique index let exactly one through
        }

        // Emit event with UserId (person ID) for ranking
        await events.DispatchAsync([new PresenceConfirmed(command.MatchId, user.UserId, familyId)], ct);

        // presence.confirmed: the other members of the family know it is done (no double confirmation), in-app only.
        await Safe.RunAsync(log, "presence confirmed", async () =>
        {
            var others = (await identity.GetUserIdsOfFamilyAsync(familyId, ct)).Where(id => id != user.UserId).ToList();
            await notifications.SendToUsersAsync(others, "Présence confirmée",
                $"La présence de la famille est confirmée pour {match?.Title ?? "le match"}.",
                new Dictionary<string, string> { ["type"] = "presence_confirmed", ["matchId"] = command.MatchId.ToString() }, ct, push: false);
        });
        return Result.Success();
    }
}
