namespace Application.Services;

using Application.Interfaces;

using Domain.Dto;

using Infrastructure.Interfaces;

using NLog;

public class NotificationService : INotificationService
{
    private readonly INotificationRepository _notifications;
    private static readonly ILogger Log = LogManager.GetCurrentClassLogger();

    public NotificationService(INotificationRepository notifications)
        => _notifications = notifications;

    public async Task<IEnumerable<NotificationDto>> GetByUserAsync(Guid userId)
    {
        Log.Debug("GetByUserAsync {UserId}", userId);
        return (await _notifications.GetByUserAsync(userId)).Select(ToDto);
    }

    public async Task<IEnumerable<NotificationDto>> GetStartupNotificationsAsync(Guid userId)
    {
        Log.Debug("GetStartupNotificationsAsync {UserId}", userId);
        return (await _notifications.GetStartupNotificationsAsync(userId)).Select(ToDto);
    }

    public async Task<int> GetUnreadCountAsync(Guid userId)
        => await _notifications.GetUnreadCountAsync(userId);

    public async Task MarkReadAsync(Guid notificationId, Guid userId)
    {
        Log.Info("MarkReadAsync {Id} user={UserId}", notificationId, userId);

        var notification = await _notifications.GetByIdAsync(notificationId)
            ?? throw new KeyNotFoundException($"Notification {notificationId} not found.");

        if (notification.UserId != userId)
            throw new UnauthorizedAccessException("Cannot mark another user's notification as read.");

        // Recursive notifications are never permanently marked read
        if (!notification.IsRecursif)
        {
            notification.IsRead = true;
            await _notifications.UpdateAsync(notification);
        }
    }

    public async Task MarkAllReadAsync(Guid userId)
    {
        Log.Info("MarkAllReadAsync {UserId}", userId);
        // Repository skips recursive notifications automatically
        await _notifications.MarkAllReadAsync(userId);
    }

    internal static NotificationDto ToDto(Domain.Entities.AppNotification n) =>
        new(n.Id, n.Type, n.Title, n.Body, n.IsRead, n.IsRecursif, n.IsShowAtStart,
            n.ExpiresAt, n.CreatedAt, n.MatchId, n.CreatedByAdminId);
}
