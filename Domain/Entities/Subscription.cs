namespace Domain.Entities;

using Domain.Enums;

public class Subscription
{
    public Guid Id { get; set; }
    public OfficialRole Role { get; set; }
    public MatchSubscriptionStatus Status { get; set; } = MatchSubscriptionStatus.Subscribed;
    public DateTime? ConfirmedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // FKs
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid MatchId { get; set; }
    public Match Match { get; set; } = null!;
}
