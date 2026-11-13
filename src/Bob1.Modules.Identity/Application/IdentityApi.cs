using Bob1.Shared.Contracts.Identity;

namespace Bob1.Modules.Identity.Application;

internal sealed class IdentityApi(IdentityDbContext db, MembershipService memberships) : IIdentityApi
{
    public async Task<MemberInfo?> GetMemberAsync(Guid userId, CancellationToken ct)
    {
        var season = await memberships.EnsureCurrentSeasonAsync(ct);
        return await db.Users.AsNoTracking().Where(u => u.Id == userId)
            .Select(u => new MemberInfo(u.Id, u.FamilyId, u.FirstName, u.LastName,
                db.Memberships.Where(m => m.UserId == u.Id && m.Season == season).Select(m => (Guid?)m.DivisionId).FirstOrDefault()))
            .SingleOrDefaultAsync(ct);
    }

    public async Task<bool> IsInTeamsAsync(Guid userId, Guid? familyId, IReadOnlyCollection<Guid> teamIds, CancellationToken ct)
    {
        var season = await memberships.EnsureCurrentSeasonAsync(ct);
        return await db.TeamMemberships.AsNoTracking().AnyAsync(t =>
            t.Season == season && teamIds.Contains(t.TeamId) &&
            db.Users.Any(u => u.Id == t.UserId && u.IsActive &&
                (u.Id == userId || (familyId != null && u.FamilyId == familyId))), ct);
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetFamilyNamesAsync(IEnumerable<Guid> familyIds, CancellationToken ct)
    {
        var ids = familyIds.Distinct().ToList();
        return await db.Families.AsNoTracking().Where(f => ids.Contains(f.Id)).ToDictionaryAsync(f => f.Id, f => f.Name, ct);
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetUserNamesAsync(IEnumerable<Guid> userIds, CancellationToken ct)
    {
        var ids = userIds.Distinct().ToList();
        var users = await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName }).ToListAsync(ct);
        return users.ToDictionary(u => u.Id, u => $"{u.FirstName} {u.LastName}".Trim());
    }

    public async Task<IReadOnlyList<Guid>> GetUserIdsByRoleAsync(string role, CancellationToken ct)
    {
        if (!Enum.TryParse<UserRoles>(role, ignoreCase: true, out var flag) || flag == UserRoles.None) return [];
        return await db.Users.AsNoTracking().Where(u => u.IsActive && (u.Roles & flag) == flag).Select(u => u.Id).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Guid>> GetPlayerIdsByTeamsAsync(IReadOnlyCollection<Guid> teamIds, CancellationToken ct)
    {
        var season = await memberships.EnsureCurrentSeasonAsync(ct);
        return await db.TeamMemberships.AsNoTracking()
            .Where(t => t.Season == season && teamIds.Contains(t.TeamId))
            .Join(db.Users.Where(u => u.IsActive), t => t.UserId, u => u.Id, (_, u) => u.Id)
            .Distinct().ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Guid>> GetUserIdsByTeamsAsync(IReadOnlyCollection<Guid> teamIds, CancellationToken ct)
    {
        var players = await GetPlayerIdsByTeamsAsync(teamIds, ct);
        if (players.Count == 0) return [];
        var familyIds = db.Users.Where(u => players.Contains(u.Id) && u.FamilyId != null).Select(u => u.FamilyId!.Value);
        return await db.Users.AsNoTracking()
            .Where(u => u.IsActive && (players.Contains(u.Id) || (u.FamilyId != null && familyIds.Contains(u.FamilyId.Value))))
            .Select(u => u.Id).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Guid>> GetUserIdsOfFamilyAsync(Guid familyId, CancellationToken ct) =>
        await db.Users.AsNoTracking().Where(u => u.FamilyId == familyId && u.IsActive).Select(u => u.Id).ToListAsync(ct);

    public async Task<IReadOnlyList<Guid>> GetUserIdsByDivisionAsync(Guid? divisionId, CancellationToken ct)
    {
        var season = await memberships.EnsureCurrentSeasonAsync(ct);
        var users = db.Users.AsNoTracking().Where(u => u.IsActive);
        if (divisionId is { } d)
        {
            var playerIds = db.Memberships.Where(m => m.Season == season && m.DivisionId == d).Select(m => m.UserId);
            var familyIds = db.Users.Where(u => u.FamilyId != null && playerIds.Contains(u.Id)).Select(u => u.FamilyId!.Value);
            users = users.Where(u => playerIds.Contains(u.Id) || (u.FamilyId != null && familyIds.Contains(u.FamilyId.Value)));
        }
        return await users.Select(u => u.Id).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Guid>> GetFamilyIdsByDivisionAsync(Guid divisionId, CancellationToken ct)
    {
        var season = await memberships.EnsureCurrentSeasonAsync(ct);
        return await db.Memberships.AsNoTracking().Where(m => m.Season == season && m.DivisionId == divisionId)
            .Join(db.Users.Where(u => u.IsActive && u.FamilyId != null), m => m.UserId, u => u.Id, (_, u) => u.FamilyId!.Value)
            .Distinct().ToListAsync(ct);
    }

    public async Task<IReadOnlyDictionary<Guid, int>> GetSeasonPlayerCountsAsync(string season, CancellationToken ct) =>
        await db.Memberships.AsNoTracking().Where(m => m.Season == season)
            .GroupBy(m => m.DivisionId).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

    // New methods for person-level rankings
    public async Task<IReadOnlyDictionary<Guid, string>> GetPersonNamesAsync(IEnumerable<Guid> personIds, CancellationToken ct)
    {
        var ids = personIds.Distinct().ToList();
        var users = await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName }).ToListAsync(ct);
        return users.ToDictionary(u => u.Id, u => $"{u.FirstName} {u.LastName}".Trim());
    }

    public async Task<IReadOnlyDictionary<Guid, Guid?>> GetPersonFamilyMapAsync(CancellationToken ct)
    {
        return await db.Users.AsNoTracking().Where(u => u.FamilyId != null)
            .ToDictionaryAsync(u => u.Id, u => u.FamilyId, ct);
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetFamilyNamesForPersonsAsync(IEnumerable<Guid> personIds, CancellationToken ct)
    {
        var ids = personIds.Distinct().ToList();
        var users = await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id) && u.FamilyId != null)
            .Select(u => new { u.Id, u.FamilyId }).ToListAsync(ct);
        
        var familyIds = users.Select(u => u.FamilyId!.Value).Distinct().ToList();
        var familyNames = await db.Families.AsNoTracking().Where(f => familyIds.Contains(f.Id))
            .ToDictionaryAsync(f => f.Id, f => f.Name, ct);
        
        var result = new Dictionary<Guid, string>();
        foreach (var user in users)
        {
            if (user.FamilyId.HasValue && familyNames.TryGetValue(user.FamilyId.Value, out var name))
                result[user.Id] = name;
        }
        return result;
    }
}
