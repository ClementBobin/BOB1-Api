namespace Bob1.Modules.Identity.Domain;

/// <summary>
/// The division a user plays in for one season (the only thing a season stores about a player).
/// At most one row per user and season. A new season starts as a copy of the previous one; editing a
/// division only ever changes the current season, so past seasons stay as they were (history).
/// </summary>
internal sealed class Membership
{
    public Guid UserId { get; private set; }
    public string Season { get; private set; } = default!;   // "2026-2027"
    public Guid DivisionId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private Membership() { }

    public static Membership Create(Guid userId, string season, Guid divisionId, DateTime nowUtc) =>
        new() { UserId = userId, Season = season, DivisionId = divisionId, CreatedAtUtc = nowUtc };

    public void MoveTo(Guid divisionId) => DivisionId = divisionId;
}
