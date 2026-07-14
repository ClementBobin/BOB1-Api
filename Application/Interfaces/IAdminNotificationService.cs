namespace Application.Interfaces;

using Domain.Dto;

public interface IAdminNotificationService
{
    /// <summary>Broadcast a notification to one user, a list of users, or all.</summary>
    Task<IEnumerable<NotificationDto>> CreateAsync(AdminCreateNotificationRequest request, Guid adminId);
    Task<NotificationDto> UpdateAsync(Guid id, UpdateNotificationRequest request, Guid adminId);
    Task DeleteAsync(Guid id, Guid adminId);
    Task<IEnumerable<NotificationDto>> GetAllAsync();
}
