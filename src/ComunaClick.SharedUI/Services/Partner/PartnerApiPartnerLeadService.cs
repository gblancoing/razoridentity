using System.Net.Http;
using ComunaClick.Shared.Api.Partner;
using ComunaClick.Shared.Partner.Interfaces;
using ComunaClick.Shared.Partner.Leads;

namespace ComunaClick.SharedUI.Services.Partner;

public sealed class PartnerApiPartnerLeadService : IPartnerLeadService
{
    private readonly PartnerApiClient _partnerApi;
    private readonly AuthStateService _authState;

    public PartnerApiPartnerLeadService(PartnerApiClient partnerApi, AuthStateService authState)
    {
        _partnerApi = partnerApi;
        _authState = authState;
    }

    public async Task<IReadOnlyList<LeadDto>> GetLeadsAsync(CancellationToken cancellationToken = default)
    {
        var partnerId = await PartnerApiServiceHelper.ResolvePartnerIdAsync(_authState, _partnerApi, cancellationToken);
        if (!partnerId.HasValue)
            return Array.Empty<LeadDto>();

        try
        {
            var leads = await _partnerApi.GetPartnerLeadsAsync(partnerId.Value, cancellationToken) ?? Array.Empty<Lead>();
            return leads
                .OrderByDescending(l => l.CreatedAt)
                .Select(l => new LeadDto(
                    l.Id,
                    $"Cliente {l.CustomerId.ToString()[..8]}…",
                    l.Message ?? "",
                    l.Status ?? "Nuevo",
                    l.CreatedAt))
                .ToList();
        }
        catch (HttpRequestException)
        {
            return Array.Empty<LeadDto>();
        }
    }
}
