namespace Application.DTOs.Notifications;

public sealed record NotificationDto(
    long Id,
    string EventType,
    string Title,
    string Message,
    bool IsRead,
    DateTime CreatedAtUtc,
    int? RequestId);
