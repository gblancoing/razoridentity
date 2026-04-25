using System.Net.Http;
using ComunaClick.Shared.Api.Partner;
using ComunaClick.Shared.Partner.Interfaces;
using ComunaClick.Shared.Partner.Notifications;

namespace ComunaClick.SharedUI.Services.Partner;

public sealed class PartnerApiPartnerNotificationService : IPartnerNotificationService
{
    private readonly PartnerApiClient _partnerApi;
    private readonly AuthStateService _authState;

    public PartnerApiPartnerNotificationService(PartnerApiClient partnerApi, AuthStateService authState)
    {
        _partnerApi = partnerApi;
        _authState = authState;
    }

    public async Task<IReadOnlyList<NotificationDto>> GetNotificationsAsync(CancellationToken cancellationToken = default)
    {
        var partnerId = await PartnerApiServiceHelper.ResolvePartnerIdAsync(_authState, _partnerApi, cancellationToken);
        if (!partnerId.HasValue)
            return Array.Empty<NotificationDto>();

        try
        {
            var rows = await _partnerApi.GetPartnerNotificationsAsync(partnerId.Value, cancellationToken) ?? Array.Empty<Notification>();
            return rows
                .OrderByDescending(n => n.CreatedAt)
                .Select(n => new NotificationDto(
                    n.Id,
                    string.IsNullOrWhiteSpace(n.Type) ? "Aviso" : n.Type!,
                    string.IsNullOrWhiteSpace(n.Payload) ? (n.ReferenceId?.ToString() ?? "") : n.Payload!,
                    n.Type ?? "general",
                    n.CreatedAt,
                    false))
                .ToList();
        }
        catch (HttpRequestException)
        {
            return Array.Empty<NotificationDto>();
        }
    }
}
