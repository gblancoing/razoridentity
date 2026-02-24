using ComunaClick.Shared.Partner.Notifications;

namespace ComunaClick.Shared.Partner.Interfaces;

public interface IPartnerNotificationService
{
    Task<IReadOnlyList<NotificationDto>> GetNotificationsAsync(CancellationToken cancellationToken = default);
}
