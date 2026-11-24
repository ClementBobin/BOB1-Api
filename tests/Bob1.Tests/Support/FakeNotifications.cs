using Bob1.Shared.Contracts.Notifications;

namespace Bob1.Tests;

/// <summary>Records what the code under test sends; everything else is a no-op.</summary>
internal sealed class FakeNotifications : INotificationsApi
{
    public List<(IReadOnlyList<Guid> Users, string Title, string Body)> Sent { get; } = [];

    public Task SendToUsersAsync(IEnumerable<Guid> userIds, string title, string body,
        IReadOnlyDictionary<string, string>? data = null, CancellationToken ct = default,
        DateTime? expiresAtUtc = null, bool recurring = false, bool startup = false, bool push = true,
        bool adminVisible = true, int? recurrenceHours = null)
    {
        Sent.Add((userIds.ToList(), title, body));
        return Task.CompletedTask;
    }

    public Task SendToPersonAsync(Guid personId, string title, string body,
        IReadOnlyDictionary<string, string>? data = null, CancellationToken ct = default,
        DateTime? expiresAtUtc = null, bool recurring = false, bool startup = false, bool push = true) =>
        Task.CompletedTask;

    public Task<int> SendToUsersOnceAsync(string dedupeKey, IEnumerable<Guid> userIds, string title, string body,
        IReadOnlyDictionary<string, string>? data = null, CancellationToken ct = default,
        DateTime? expiresAtUtc = null, bool push = true) =>
        Task.FromResult(0);

    public Task NotifyAdminsAsync(string title, string body, IReadOnlyDictionary<string, string>? data = null,
        Guid? exceptUserId = null, CancellationToken ct = default) =>
        Task.CompletedTask;
}
