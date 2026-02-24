namespace ComunaClick.Shared.Partner.Notifications;

public sealed record NotificationDto(
    Guid Id,
    string Title,
    string Message,
    string Type,
    DateTimeOffset CreatedAt,
    bool IsRead
);
