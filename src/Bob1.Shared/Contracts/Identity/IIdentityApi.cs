namespace Bob1.Shared.Contracts.Identity;

/// <summary>What other modules may ask Identity. Everything else about users/families stays private.</summary>
public interface IIdentityApi
{
    /// <summary>A family member (parent or child: they all live in the users table).</summary>
    Task<MemberInfo?> GetMemberAsync(Guid userId, CancellationToken ct);
    Task<IReadOnlyDictionary<Guid, string>> GetFamilyNamesAsync(IEnumerable<Guid> familyIds, CancellationToken ct);
    Task<IReadOnlyDictionary<Guid, string>> GetUserNamesAsync(IEnumerable<Guid> userIds, CancellationToken ct);
    Task<IReadOnlyList<Guid>> GetUserIdsOfFamilyAsync(Guid familyId, CancellationToken ct);
    /// <summary>Users of this division (current season) plus the other members of their families. Null division = every active user.</summary>
    Task<IReadOnlyList<Guid>> GetUserIdsByDivisionAsync(Guid? divisionId, CancellationToken ct);
    /// <summary>Families that have at least one active member in this division (current season).</summary>
    Task<IReadOnlyList<Guid>> GetFamilyIdsByDivisionAsync(Guid divisionId, CancellationToken ct);
    /// <summary>
    /// True when this user, or a member of this family, is a player of one of these teams this season
    /// (a player can be in several teams; teams are stored per season).
    /// </summary>
    Task<bool> IsInTeamsAsync(Guid userId, Guid? familyId, IReadOnlyCollection<Guid> teamIds, CancellationToken ct);
    /// <summary>Active users holding this role ("Parent", "Staff" or "Admin").</summary>
    Task<IReadOnlyList<Guid>> GetUserIdsByRoleAsync(string role, CancellationToken ct);
    /// <summary>Players of these teams (current season) plus the other members of their families.</summary>
    Task<IReadOnlyList<Guid>> GetUserIdsByTeamsAsync(IReadOnlyCollection<Guid> teamIds, CancellationToken ct);
    /// <summary>Players of these teams (current season), without their families.</summary>
    Task<IReadOnlyList<Guid>> GetPlayerIdsByTeamsAsync(IReadOnlyCollection<Guid> teamIds, CancellationToken ct);
    /// <summary>Players per division for a season (history).</summary>
    Task<IReadOnlyDictionary<Guid, int>> GetSeasonPlayerCountsAsync(string season, CancellationToken ct);
    
    // New methods for person-level rankings
    Task<IReadOnlyDictionary<Guid, string>> GetPersonNamesAsync(IEnumerable<Guid> personIds, CancellationToken ct);
    Task<IReadOnlyDictionary<Guid, Guid?>> GetPersonFamilyMapAsync(CancellationToken ct);
    Task<IReadOnlyDictionary<Guid, string>> GetFamilyNamesForPersonsAsync(IEnumerable<Guid> personIds, CancellationToken ct);
}

public sealed record MemberInfo(Guid Id, Guid? FamilyId, string FirstName, string LastName, Guid? DivisionId);   // DivisionId = division in the current season
