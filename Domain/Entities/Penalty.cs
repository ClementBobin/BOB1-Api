namespace Domain.Entities;

public class Penalty
{
    public Guid Id { get; set; }
    public string Reason { get; set; } = string.Empty;
    public int Points { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // FKs
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid? MatchId { get; set; }
    public Match? Match { get; set; }
}
