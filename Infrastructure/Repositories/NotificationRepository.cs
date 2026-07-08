using Domain.Entities;
using Infrastructure.Data;
using Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using NLog;

namespace Infrastructure.Repositories;

public class NotificationRepository : INotificationRepository
{
    private readonly AppDbContext _db;
    private static readonly ILogger Log = LogManager.GetCurrentClassLogger();

    public NotificationRepository(AppDbContext db) => _db = db;

    public async Task<AppNotification?> GetByIdAsync(Guid id)
    {
        Log.Debug("GetByIdAsync {Id}", id);
        return await _db.Notifications
            .Include(n => n.Match)
            .FirstOrDefaultAsync(n => n.Id == id);
    }

    public async Task<IEnumerable<AppNotification>> GetByUserAsync(Guid userId)
    {
        Log.Debug("GetByUserAsync {UserId}", userId);
        return await _db.Notifications
            .AsNoTracking()
            .Include(n => n.Match)
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();
    }

    public async Task<int> GetUnreadCountAsync(Guid userId)
    {
        Log.Debug("GetUnreadCountAsync {UserId}", userId);
        return await _db.Notifications
            .CountAsync(n => n.UserId == userId && !n.IsRead);
    }

    public async Task AddAsync(AppNotification notification)
    {
        Log.Info("AddAsync user={UserId} type={Type}", notification.UserId, notification.Type);
        await _db.Notifications.AddAsync(notification);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(AppNotification notification)
    {
        Log.Info("UpdateAsync {Id} isRead={IsRead}", notification.Id, notification.IsRead);
        _db.Notifications.Update(notification);
        await _db.SaveChangesAsync();
    }

    public async Task MarkAllReadAsync(Guid userId)
    {
        Log.Info("MarkAllReadAsync {UserId}", userId);
        await _db.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
    }
}
