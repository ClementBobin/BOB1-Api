using Bob1.Shared.Contracts.Club;

namespace Bob1.Modules.Club.Application;

internal sealed class ClubApi(ClubDbContext db) : IClubApi
{
    public Task<DivisionInfo?> GetDivisionAsync(Guid id, CancellationToken ct) =>
        db.Divisions.AsNoTracking().Where(d => d.Id == id)
            .Select(d => new DivisionInfo(d.Id, d.Name)).SingleOrDefaultAsync(ct);

    public Task<VenueInfo?> GetVenueAsync(Guid id, CancellationToken ct) =>
        db.Venues.AsNoTracking().Where(v => v.Id == id)
            .Select(v => new VenueInfo(v.Id, v.Name)).SingleOrDefaultAsync(ct);

    public async Task<IReadOnlyDictionary<Guid, string>> GetDivisionNamesAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();
        return await db.Divisions.AsNoTracking().Where(d => list.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, d => d.Name, ct);
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetTeamNamesAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();
        return await db.Teams.AsNoTracking().Where(t => list.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.Name, ct);
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetVenueNamesAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();
        return await db.Venues.AsNoTracking().Where(v => list.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, v => v.Name, ct);
    }

    public async Task<ClubSearchHits> SearchAsync(string search, CancellationToken ct)
    {
        if (Paging.Pattern(search) is not { } like) return new([], [], []);
        var divisions = await db.Divisions.AsNoTracking().Where(d => EF.Functions.ILike(d.Name, like, "\\")).Select(d => d.Id).ToListAsync(ct);
        var teams = await db.Teams.AsNoTracking().Where(t => EF.Functions.ILike(t.Name, like, "\\")).Select(t => t.Id).ToListAsync(ct);
        var venues = await db.Venues.AsNoTracking().Where(v => EF.Functions.ILike(v.Name, like, "\\")).Select(v => v.Id).ToListAsync(ct);
        return new(divisions, teams, venues);
    }
}
