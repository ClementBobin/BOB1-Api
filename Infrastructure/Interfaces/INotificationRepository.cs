namespace Infrastructure.Interfaces;

using Domain.Entities;

public interface INotificationRepository
{
    Task<AppNotification?> GetByIdAsync(Guid id);
    Task<IEnumerable<AppNotification>> GetByUserAsync(Guid userId);
    Task<IEnumerable<AppNotification>> GetStartupNotificationsAsync(Guid userId);
    Task<int> GetUnreadCountAsync(Guid userId);
    Task<IEnumerable<AppNotification>> GetAllAdminCreatedAsync();
    Task AddAsync(AppNotification notification);
    Task AddRangeAsync(IEnumerable<AppNotification> notifications);
    Task UpdateAsync(AppNotification notification);
    Task DeleteAsync(Guid id);
    Task MarkAllReadAsync(Guid userId);
}
