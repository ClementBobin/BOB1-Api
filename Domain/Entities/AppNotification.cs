namespace Domain.Entities;

using Domain.Enums;

public class AppNotification
{
    public Guid Id { get; set; }
    public NotificationType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool IsRead { get; set; }

    /// <summary>If true, the notification reappears on every app startup regardless of IsRead.</summary>
    public bool IsRecursif { get; set; }

    /// <summary>If true, the notification is shown immediately on startup (modal/banner).</summary>
    public bool IsShowAtStart { get; set; }

    /// <summary>
    /// Expiration date. For admin-created and recursive notifications only.
    /// Once expired the notification is hidden from the user even if unread.
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // FKs
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid? MatchId { get; set; }
    public Match? Match { get; set; }

    /// <summary>Set when the notification was created by an admin (null for system-generated).</summary>
    public Guid? CreatedByAdminId { get; set; }
    public User? CreatedByAdmin { get; set; }
}
