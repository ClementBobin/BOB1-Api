namespace Bob1.Modules.Notifications.Domain;

internal sealed class DeviceToken
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public string Token { get; private set; } = default!;
    public DateTime LastSeenAtUtc { get; private set; }

    private DeviceToken() { }

    public static DeviceToken Create(Guid userId, string token, DateTime nowUtc) =>
        new() { UserId = userId, Token = token, LastSeenAtUtc = nowUtc };

    public void Reassign(Guid userId, DateTime nowUtc)
    {
        UserId = userId; LastSeenAtUtc = nowUtc;
    }
}

/// <summary>
/// One row per notification sent (the content lives here, once). Recipients are <see cref="UserNotification"/> rows.
/// The admin list only shows rows with <see cref="VisibleInAdmin"/> = true (broadcasts, match notices...);
/// personal notices (penalty, bonus, reminders, audit copies) stay visible to their recipients only.
/// Inside this module prefer the full name when FirebaseAdmin.Messaging is imported (it has its own Notification type).
/// </summary>
internal sealed class Notification
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Type { get; private set; } = "broadcast";
    public string Title { get; private set; } = default!;
    public string Body { get; private set; } = default!;
    public string? DataJson { get; private set; }
    /// <summary>Shared de-duplication key (type + entity + period). Unique when set.</summary>
    public string? DedupeKey { get; private set; }
    public Guid? CreatedByUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ExpiresAtUtc { get; private set; }
    public bool Recurring { get; private set; }
    public bool Startup { get; private set; }
    public bool VisibleInAdmin { get; private set; }

    private Notification() { }

    public static Notification Create(string type, string title, string body, string? dataJson, DateTime nowUtc,
        bool visibleInAdmin, Guid? createdByUserId = null, string? dedupeKey = null,
        DateTime? expiresAtUtc = null, bool recurring = false, bool startup = false) =>
        new() { Type = type, Title = title, Body = body, DataJson = dataJson, CreatedAtUtc = nowUtc,
            VisibleInAdmin = visibleInAdmin, CreatedByUserId = createdByUserId, DedupeKey = dedupeKey,
            ExpiresAtUtc = expiresAtUtc, Recurring = recurring, Startup = startup };

    public void Update(string title, string body, DateTime? expiresAtUtc, bool recurring, bool startup)
    {
        Title = title;
        Body = body;
        ExpiresAtUtc = expiresAtUtc;
        Recurring = recurring;
        Startup = startup;
    }
}

/// <summary>Delivery + read state of a <see cref="Notification"/> for one user. Holds no content.</summary>
internal sealed class UserNotification
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid NotificationId { get; private set; }
    public Guid UserId { get; private set; }
    /// <summary>When this user received it (a de-duplicated reminder can reach new users later).</summary>
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ReadAtUtc { get; private set; }

    private UserNotification() { }

    public static UserNotification Create(Guid notificationId, Guid userId, DateTime nowUtc) =>
        new() { NotificationId = notificationId, UserId = userId, CreatedAtUtc = nowUtc };

    public void MarkRead(DateTime nowUtc) => ReadAtUtc ??= nowUtc;
}
