namespace Application.Interfaces;

using Domain.Dto;

public interface INotificationService
{
    Task<IEnumerable<NotificationDto>> GetByUserAsync(Guid userId);
    Task<IEnumerable<NotificationDto>> GetStartupNotificationsAsync(Guid userId);
    Task<int> GetUnreadCountAsync(Guid userId);
    Task MarkReadAsync(Guid notificationId, Guid userId);
    Task MarkAllReadAsync(Guid userId);
}
