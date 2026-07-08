namespace Domain.Dto;

using Domain.Enums;

public record NotificationDto(
    Guid Id,
    NotificationType Type,
    string Title,
    string Body,
    bool IsRead,
    DateTime CreatedAt,
    Guid? MatchId);

public record CreateNotificationRequest(
    CreateNotificationType Type,
    string Title,
    string Body);
