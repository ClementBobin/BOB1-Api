using Bob1.Shared.Contracts.Privacy;

namespace Bob1.Modules.Participation.Application;

internal sealed class ParticipationPersonalData(ParticipationDbContext db) : IPersonalDataContributor
{
    public string Section => "participation";

    public async Task<object> ExportAsync(PersonalDataSubject s, CancellationToken ct)
    {
        var confirmations = await db.PresenceConfirmations.AsNoTracking().Where(c => c.ConfirmedByUserId == s.UserId)
            .OrderBy(c => c.ConfirmedAtUtc).Select(c => new { c.MatchId, c.ConfirmedAtUtc }).ToListAsync(ct);
        var signups = await db.SlotSignups.AsNoTracking().Where(x => x.UserId == s.UserId)
            .Join(db.Slots, x => x.SlotId, sl => sl.Id, (x, sl) => new { sl.MatchId, sl.Role, x.CreatedAtUtc })
            .OrderBy(x => x.CreatedAtUtc).ToListAsync(ct);
        return new { presenceConfirmations = confirmations, slotSignups = signups };
    }

    public async Task EraseAsync(PersonalDataSubject s, CancellationToken ct)
    {
        // Through the aggregate so the slot's Filled counter stays right. The SlotLeft event is deliberately not dispatched:
        // the person's points are erased below, and nobody needs "X left the slot" about an account that no longer exists.
        var slots = await db.Slots.Include(x => x.Signups).Where(x => x.Signups.Any(y => y.UserId == s.UserId)).ToListAsync(ct);
        foreach (var slot in slots) slot.Leave(s.UserId);
        await db.SaveChangesAsync(ct);

        // A family's confirmation outlives one of its parents: keep the row, drop who clicked.
        await db.PresenceConfirmations.Where(c => c.ConfirmedByUserId == s.UserId)
            .ExecuteUpdateAsync(u => u.SetProperty(c => c.ConfirmedByUserId, Guid.Empty), ct);
        if (s.FamilyEmptied && s.FamilyId is { } family)
            await db.PresenceConfirmations.Where(c => c.FamilyId == family).ExecuteDeleteAsync(ct);
    }
}
