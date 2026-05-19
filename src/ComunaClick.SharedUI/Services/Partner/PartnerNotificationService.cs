using ComunaClick.Shared.Partner.Interfaces;
using ComunaClick.Shared.Partner.Notifications;
using ComunaClick.SharedUI.Services;

namespace ComunaClick.SharedUI.Services.Partner;

public sealed class PartnerNotificationService : PartnerServiceBase, IPartnerNotificationService
{
    public PartnerNotificationService(ComunaClick.Shared.Api.Partner.PartnerApiClient partnerApi, AuthStateService authState)
        : base(partnerApi, authState)
    {
    }

    public async Task<IReadOnlyList<NotificationDto>> GetNotificationsAsync(CancellationToken cancellationToken = default)
    {
        var partnerId = await ResolvePartnerIdAsync(cancellationToken);
        if (!partnerId.HasValue)
        {
            return Array.Empty<NotificationDto>();
        }

        var notifications = await PartnerApi.GetPartnerNotificationsAsync(partnerId.Value, false, cancellationToken) ?? [];
        return notifications
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new NotificationDto(
                x.Id,
                BuildNotificationTitle(x),
                x.Payload ?? "Notificación operativa",
                x.Type ?? "system",
                x.CreatedAt,
                x.ReadAt.HasValue))
            .ToList();
    }

    private static string BuildNotificationTitle(ComunaClick.Shared.Api.Partner.Notification notification)
        => notification.Type?.Trim().ToLowerInvariant() switch
        {
            "order" => "Nuevo pedido",
            "booking" => "Reserva actualizada",
            "lead" => "Lead actualizado",
            "payout" => "Payout actualizado",
            _ => "Actualización operativa"
        };
}
