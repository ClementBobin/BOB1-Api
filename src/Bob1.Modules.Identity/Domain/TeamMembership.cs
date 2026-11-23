namespace Bob1.Modules.Identity.Domain;

/// <summary>
/// A team a user plays in for one season. A player can be in several teams; like <see cref="Membership"/>,
/// a new season starts as a copy of the previous one and edits only touch the current season.
/// </summary>
internal sealed class TeamMembership
{
    public Guid UserId { get; private set; }
    public string Season { get; private set; } = default!;   // "2026-2027"
    public Guid TeamId { get; private set; }

    private TeamMembership() { }

    public static TeamMembership Create(Guid userId, string season, Guid teamId) =>
        new() { UserId = userId, Season = season, TeamId = teamId };
}
