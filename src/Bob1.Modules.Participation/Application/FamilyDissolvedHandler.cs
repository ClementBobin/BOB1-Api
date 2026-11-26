using Bob1.Shared.Contracts.Identity;

namespace Bob1.Modules.Participation.Application;

/// <summary>A family with no member left has nobody to confirm presence for: its confirmations are dropped (a new family must not inherit them).</summary>
internal sealed class FamilyDissolvedHandler(ParticipationDbContext db, ILogger<FamilyDissolvedHandler> log)
    : IDomainEventHandler<FamilyDissolved>
{
    public Task HandleAsync(FamilyDissolved e, CancellationToken ct) => Safe.RunAsync(log, "family dissolved (presence)", async () =>
        await db.PresenceConfirmations.Where(c => c.FamilyId == e.FamilyId).ExecuteDeleteAsync(ct));
}
