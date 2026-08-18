namespace Domain.Entities;

public class SeasonPoint
{
    public int SeasonId { get; set; } // year season starts, e.g. 2024 = 01/09/2024 → 31/08/2025
    public Guid Id { get; set; }
    public int Points { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid? MatchId { get; set; }
    public Match? Match { get; set; }
}