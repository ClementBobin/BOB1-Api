using Bob1.Shared.Contracts.Carpool;

namespace Bob1.Modules.Carpool.Application;

internal sealed class CarpoolApi(CarpoolDbContext db) : ICarpoolApi
{
    public async Task<IReadOnlyList<CarpoolCommitment>> GetCommitmentsAsync(Guid userId, DateTime fromUtc, CancellationToken ct)
    {
        var driving = await db.Rides.AsNoTracking()
            .Where(r => r.DriverUserId == userId && r.DepartureUtc >= fromUtc)
            .Select(r => new CarpoolCommitment(r.Id, r.MatchId, true, r.Seats, r.DepartureUtc))
            .ToListAsync(ct);

        var riding = await db.Rides.AsNoTracking()
            .Where(r => r.DepartureUtc >= fromUtc && r.Bookings.Any(b => b.UserId == userId))
            .Select(r => new CarpoolCommitment(r.Id, r.MatchId, false, 1, r.DepartureUtc))
            .ToListAsync(ct);

        return driving.Concat(riding).OrderBy(c => c.DepartureUtc).ToList();
    }
}
