namespace Bob1.Shared.Contracts.Notifications;

/// <summary>
/// Stores an in-app notification for each user and (unless push is false) pushes it to their devices.
/// Channels: in-app is always written (low level); push is the medium level; there is no email except account security.
/// </summary>
public interface INotificationsApi
{
    Task SendToUsersAsync(IEnumerable<Guid> userIds, string title, string body,
        IReadOnlyDictionary<string, string>? data = null, CancellationToken ct = default,
        DateTime? expiresAtUtc = null, bool recurring = false, bool startup = false, bool push = true);

    Task SendToPersonAsync(Guid personId, string title, string body,
        IReadOnlyDictionary<string, string>? data = null, CancellationToken ct = default,
        DateTime? expiresAtUtc = null, bool recurring = false, bool startup = false, bool push = true);

    /// <summary>
    /// Audit copy for every active admin: in-app only (no push), skipping the admin who triggered the action.
    /// </summary>
    Task NotifyAdminsAsync(string title, string body, IReadOnlyDictionary<string, string>? data = null,
        Guid? exceptUserId = null, CancellationToken ct = default);
}
