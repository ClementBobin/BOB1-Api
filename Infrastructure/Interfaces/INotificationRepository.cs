namespace Infrastructure.Interfaces;

using Domain.Entities;

public interface INotificationRepository
{
    Task<AppNotification?> GetByIdAsync(Guid id);
    Task<IEnumerable<AppNotification>> GetByUserAsync(Guid userId);
    Task<int> GetUnreadCountAsync(Guid userId);
    Task AddAsync(AppNotification notification);
    Task UpdateAsync(AppNotification notification);
    Task MarkAllReadAsync(Guid userId);
}
