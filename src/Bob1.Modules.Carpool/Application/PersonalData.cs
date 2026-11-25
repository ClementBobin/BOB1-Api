using Bob1.Shared.Contracts.Privacy;

namespace Bob1.Modules.Carpool.Application;

internal sealed class CarpoolPersonalData(CarpoolDbContext db) : IPersonalDataContributor
{
    public string Section => "carpool";

    public async Task<object> ExportAsync(PersonalDataSubject s, CancellationToken ct)
    {
        var offered = await db.Rides.AsNoTracking().Where(r => r.DriverUserId == s.UserId).OrderBy(r => r.DepartureUtc)
            .Select(r => new { r.MatchId, r.Seats, r.DepartureUtc, r.Notes, r.CreatedAtUtc }).ToListAsync(ct);
        var booked = await db.Bookings.AsNoTracking().Where(b => b.UserId == s.UserId)
            .Join(db.Rides, b => b.RideId, r => r.Id, (b, r) => new { r.MatchId, r.DepartureUtc, BookedAtUtc = b.CreatedAtUtc })
            .OrderBy(x => x.DepartureUtc).ToListAsync(ct);
        return new { ridesOffered = offered, ridesBooked = booked };
    }

    public async Task EraseAsync(PersonalDataSubject s, CancellationToken ct)
    {
        await db.Bookings.Where(b => b.UserId == s.UserId).ExecuteDeleteAsync(ct);
        // The rides the person drove go with them (the database cascades to their passengers' bookings; the passengers of
        // upcoming rides were told when the account was deactivated).
        await db.Rides.Where(r => r.DriverUserId == s.UserId).ExecuteDeleteAsync(ct);
    }
}
