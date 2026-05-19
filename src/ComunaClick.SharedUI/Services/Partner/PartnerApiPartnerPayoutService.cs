using System.Net.Http;
using ComunaClick.Shared.Api.Partner;
using ComunaClick.Shared.Partner.Interfaces;
using ComunaClick.Shared.Partner.Payouts;

namespace ComunaClick.SharedUI.Services.Partner;

public sealed class PartnerApiPartnerPayoutService : IPartnerPayoutService
{
    private readonly PartnerApiClient _partnerApi;
    private readonly AuthStateService _authState;

    public PartnerApiPartnerPayoutService(PartnerApiClient partnerApi, AuthStateService authState)
    {
        _partnerApi = partnerApi;
        _authState = authState;
    }

    public async Task<IReadOnlyList<PayoutDto>> GetPayoutsAsync(CancellationToken cancellationToken = default)
    {
        var partnerId = await PartnerApiServiceHelper.ResolvePartnerIdAsync(_authState, _partnerApi, cancellationToken);
        if (!partnerId.HasValue)
            return Array.Empty<PayoutDto>();

        try
        {
            var items = await _partnerApi.GetPartnerPayoutsAsync(partnerId.Value, cancellationToken: cancellationToken) ?? Array.Empty<PayoutItem>();
            return items
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new PayoutDto(
                    p.Id,
                    $"Lote {p.BatchId.ToString()[..8]}",
                    (decimal)p.NetAmount,
                    "Registrado",
                    p.CreatedAt))
                .ToList();
        }
        catch (HttpRequestException)
        {
            return Array.Empty<PayoutDto>();
        }
    }
}
