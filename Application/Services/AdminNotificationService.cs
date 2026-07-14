namespace Application.Services;

using Application.Interfaces;

using Domain.Dto;
using Domain.Entities;
using Domain.Enums;

using Infrastructure.Interfaces;

using NLog;

public class AdminNotificationService : IAdminNotificationService
{
    private readonly INotificationRepository _notifications;
    private readonly IUserRepository _users;
    private static readonly ILogger Log = LogManager.GetCurrentClassLogger();

    public AdminNotificationService(INotificationRepository notifications, IUserRepository users)
    {
        _notifications = notifications;
        _users = users;
    }

    public async Task<IEnumerable<NotificationDto>> GetAllAsync()
    {
        Log.Debug("GetAllAsync (admin)");
        // Return all notifications created by any admin (CreatedByAdminId is set)
        var all = await _notifications.GetAllAdminCreatedAsync();
        return all.Select(NotificationService.ToDto);
    }

    public async Task<IEnumerable<NotificationDto>> CreateAsync(
        AdminCreateNotificationRequest request, Guid adminId)
    {
        Log.Info("CreateAsync scope={Scope} admin={AdminId}", request.Scope, adminId);

        var targetUsers = await ResolveTargetsAsync(request.Scope, request.TargetUserIds);

        var notificationType = request.Type switch
        {
            CreateNotificationType.Emergency => NotificationType.Emergency,
            CreateNotificationType.General => NotificationType.General,
            _ => NotificationType.General,
        };

        var notifications = targetUsers.Select(user => new AppNotification
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Type = notificationType,
            Title = request.Title,
            Body = request.Body,
            IsRecursif = request.IsRecursif,
            IsShowAtStart = request.IsShowAtStart,
            ExpiresAt = request.ExpiresAt,
            MatchId = request.MatchId,
            CreatedByAdminId = adminId,
            CreatedAt = DateTime.UtcNow,
        }).ToList();

        await _notifications.AddRangeAsync(notifications);

        Log.Info("Created {Count} notifications for scope={Scope}", notifications.Count, request.Scope);
        return notifications.Select(NotificationService.ToDto);
    }

    public async Task<NotificationDto> UpdateAsync(Guid id, UpdateNotificationRequest request, Guid adminId)
    {
        Log.Info("UpdateAsync {Id} admin={AdminId}", id, adminId);

        var notification = await _notifications.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Notification {id} not found.");

        if (notification.CreatedByAdminId != adminId)
            throw new UnauthorizedAccessException("You can only edit notifications you created.");

        notification.Title = request.Title;
        notification.Body = request.Body;
        notification.IsRecursif = request.IsRecursif;
        notification.IsShowAtStart = request.IsShowAtStart;
        notification.ExpiresAt = request.ExpiresAt;

        await _notifications.UpdateAsync(notification);
        return NotificationService.ToDto(notification);
    }

    public async Task DeleteAsync(Guid id, Guid adminId)
    {
        Log.Info("DeleteAsync {Id} admin={AdminId}", id, adminId);

        var notification = await _notifications.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Notification {id} not found.");

        if (notification.CreatedByAdminId != adminId)
            throw new UnauthorizedAccessException("You can only delete notifications you created.");

        await _notifications.DeleteAsync(id);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private async Task<IEnumerable<Domain.Entities.User>> ResolveTargetsAsync(
        ScopeType scope, IEnumerable<Guid>? targetUserIds)
    {
        var all = (await _users.GetAllAsync()).ToList();

        return scope switch
        {
            ScopeType.All => all,
            ScopeType.Referes => all.Where(u => u.Role == UserRole.Official),
            ScopeType.User => targetUserIds is null || !targetUserIds.Any()
                ? throw new ArgumentException("TargetUserIds required when Scope is User.")
                : all.Where(u => targetUserIds.Contains(u.Id)),
            _ => throw new ArgumentException($"Unknown scope {scope}."),
        };
    }
}
