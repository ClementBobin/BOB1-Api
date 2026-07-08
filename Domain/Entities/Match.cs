namespace Domain.Entities;

public class Match
{
    public Guid Id { get; set; }
    public DateTime DateUtc { get; set; }
    public DateTime? EmergencyDateUtc { get; set; }
    public int EmergencyPoints { get; set; }

    // FKs
    public Guid DivisionId { get; set; }
    public Division Division { get; set; } = null!;

    public Guid HomeTeamId { get; set; }
    public Team HomeTeam { get; set; } = null!;

    public Guid AwayTeamId { get; set; }
    public Team AwayTeam { get; set; } = null!;

    public Guid LocationId { get; set; }
    public Location Location { get; set; } = null!;

    // Owned collection — one row per slot
    public ICollection<RoleSlot> Slots { get; set; } = [];

    // Navigation
    public ICollection<Subscription> Subscriptions { get; set; } = [];
    public ICollection<AppNotification> Notifications { get; set; } = [];
}
