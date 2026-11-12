namespace Bob1.Shared.Contracts.Club;

public interface IClubApi
{
    Task<DivisionInfo?> GetDivisionAsync(Guid id, CancellationToken ct);
    Task<VenueInfo?> GetVenueAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyDictionary<Guid, string>> GetDivisionNamesAsync(IEnumerable<Guid> ids, CancellationToken ct);
    Task<IReadOnlyDictionary<Guid, string>> GetTeamNamesAsync(IEnumerable<Guid> ids, CancellationToken ct);
    Task<IReadOnlyDictionary<Guid, string>> GetVenueNamesAsync(IEnumerable<Guid> ids, CancellationToken ct);
    /// <summary>Ids of the divisions, teams and venues whose name matches the search text (case-insensitive "contains").</summary>
    Task<ClubSearchHits> SearchAsync(string search, CancellationToken ct);
}

public sealed record ClubSearchHits(IReadOnlyList<Guid> DivisionIds, IReadOnlyList<Guid> TeamIds, IReadOnlyList<Guid> VenueIds);

public sealed record DivisionInfo(Guid Id, string Name);
public sealed record VenueInfo(Guid Id, string Name);

/// <summary>Fixed ids used by the Development seed, so other modules' seeds can reference them.</summary>
public static class ClubSeed
{
    public static readonly Guid DivisionU13M = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    public static readonly Guid DivisionU17F = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");
    public static readonly Guid TeamU13MBob1 = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
    public static readonly Guid TeamU13MCastelnau = Guid.Parse("cccccccc-0000-0000-0000-000000000002");
    public static readonly Guid VenueCoubertin = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
}
