namespace Domain.Entities;

using Domain.Enums;

public class AppNotification
{
    public Guid Id { get; set; }
    public NotificationType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // FKs
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid? MatchId { get; set; }
    public Match? Match { get; set; }
}
