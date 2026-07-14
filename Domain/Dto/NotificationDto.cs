namespace Domain.Dto;

using Domain.Enums;

public record NotificationDto(
    Guid Id,
    NotificationType Type,
    string Title,
    string Body,
    bool IsRead,
    bool IsRecursif,
    bool IsShowAtStart,
    DateTime? ExpiresAt,
    DateTime CreatedAt,
    Guid? MatchId,
    Guid? CreatedByAdminId);

/// <summary>Admin creates a notification and broadcasts it to a scope.</summary>
public record AdminCreateNotificationRequest(
    CreateNotificationType Type,
    string Title,
    string Body,
    bool IsRecursif,
    bool IsShowAtStart,
    DateTime? ExpiresAt,
    ScopeType Scope,
    /// <summary>Required when Scope = User. Can be one or many.</summary>
    IEnumerable<Guid>? TargetUserIds,
    Guid? MatchId);

public record UpdateNotificationRequest(
    string Title,
    string Body,
    bool IsRecursif,
    bool IsShowAtStart,
    DateTime? ExpiresAt);
