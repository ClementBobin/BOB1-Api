namespace Infrastructure.Repositories;

using Domain.Entities;

using Infrastructure.Data;
using Infrastructure.Interfaces;

using Microsoft.EntityFrameworkCore;

using NLog;

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
        var now = DateTime.UtcNow;
        return await _db.Notifications
            .AsNoTracking()
            .Include(n => n.Match)
            .Where(n => n.UserId == userId
                && (n.ExpiresAt == null || n.ExpiresAt > now))
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<AppNotification>> GetStartupNotificationsAsync(Guid userId)
    {
        Log.Debug("GetStartupNotificationsAsync {UserId}", userId);
        var now = DateTime.UtcNow;
        return await _db.Notifications
            .AsNoTracking()
            .Include(n => n.Match)
            .Where(n => n.UserId == userId
                && n.IsShowAtStart
                && (n.ExpiresAt == null || n.ExpiresAt > now)
                // Recursive: always show. Non-recursive: only if unread.
                && (n.IsRecursif || !n.IsRead))
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();
    }

    public async Task<int> GetUnreadCountAsync(Guid userId)
    {
        Log.Debug("GetUnreadCountAsync {UserId}", userId);
        var now = DateTime.UtcNow;
        return await _db.Notifications
            .CountAsync(n => n.UserId == userId
                && !n.IsRead
                && (n.ExpiresAt == null || n.ExpiresAt > now));
    }

    public async Task<IEnumerable<AppNotification>> GetAllAdminCreatedAsync()
    {
        Log.Debug("GetAllAdminCreatedAsync");
        return await _db.Notifications
            .AsNoTracking()
            .Include(n => n.Match)
            .Where(n => n.CreatedByAdminId != null)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();
    }

    public async Task AddAsync(AppNotification notification)
    {
        Log.Info("AddAsync user={UserId} type={Type}", notification.UserId, notification.Type);
        await _db.Notifications.AddAsync(notification);
        await _db.SaveChangesAsync();
    }

    public async Task AddRangeAsync(IEnumerable<AppNotification> notifications)
    {
        var list = notifications.ToList();
        Log.Info("AddRangeAsync count={Count}", list.Count);
        await _db.Notifications.AddRangeAsync(list);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(AppNotification notification)
    {
        Log.Info("UpdateAsync {Id}", notification.Id);
        _db.Notifications.Update(notification);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        Log.Info("DeleteAsync {Id}", id);
        var n = await _db.Notifications.FindAsync(id);
        if (n is null) return;
        _db.Notifications.Remove(n);
        await _db.SaveChangesAsync();
    }

    public async Task MarkAllReadAsync(Guid userId)
    {
        Log.Info("MarkAllReadAsync {UserId}", userId);
        await _db.Notifications
            .Where(n => n.UserId == userId && !n.IsRead && !n.IsRecursif)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
    }
}
