using System.Net.Http;
using ComunaClick.Shared.Api.Partner;

namespace ComunaClick.SharedUI.Services.Partner;

/// <summary>
/// Estado compartido del contador de notificaciones no leídas del partner.
/// Lo consumen el header (campana), el sidebar y el nav móvil; la página de
/// notificaciones fuerza un refresh al marcar leído/archivar.
/// </summary>
public sealed class PartnerNotificationsBadgeService
{
    private static readonly TimeSpan MinRefreshInterval = TimeSpan.FromSeconds(30);

    private readonly PartnerApiClient _partnerApi;
    private DateTimeOffset _lastRefresh = DateTimeOffset.MinValue;
    private Guid? _partnerId;

    public PartnerNotificationsBadgeService(PartnerApiClient partnerApi)
    {
        _partnerApi = partnerApi;
    }

    public int UnreadCount { get; private set; }

    public event Action? OnChange;

    public async Task RefreshAsync(Guid partnerId, bool force = false, CancellationToken cancellationToken = default)
    {
        if (partnerId == Guid.Empty)
        {
            return;
        }

        if (!force
            && partnerId == _partnerId
            && DateTimeOffset.UtcNow - _lastRefresh < MinRefreshInterval)
        {
            return;
        }

        _partnerId = partnerId;
        _lastRefresh = DateTimeOffset.UtcNow;

        try
        {
            var rows = await _partnerApi.GetPartnerNotificationsAsync(partnerId, includeArchived: false, cancellationToken) ?? [];
            var unread = rows.Count(x => x.ReadAt is null && x.ArchivedAt is null);
            if (unread != UnreadCount)
            {
                UnreadCount = unread;
                OnChange?.Invoke();
            }
        }
        catch (HttpRequestException)
        {
            // Sin red o sesión inválida: se conserva el último valor conocido.
        }
    }

    public void Reset()
    {
        _partnerId = null;
        _lastRefresh = DateTimeOffset.MinValue;
        if (UnreadCount != 0)
        {
            UnreadCount = 0;
            OnChange?.Invoke();
        }
    }
}
