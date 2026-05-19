using ComunaClick.Shared.Partner.Interfaces;
using ComunaClick.Shared.Partner.Leads;
using ComunaClick.SharedUI.Services;

namespace ComunaClick.SharedUI.Services.Partner;

public sealed class PartnerLeadService : PartnerServiceBase, IPartnerLeadService
{
    public PartnerLeadService(ComunaClick.Shared.Api.Partner.PartnerApiClient partnerApi, AuthStateService authState)
        : base(partnerApi, authState)
    {
    }

    public async Task<IReadOnlyList<LeadDto>> GetLeadsAsync(CancellationToken cancellationToken = default)
    {
        var partnerId = await ResolvePartnerIdAsync(cancellationToken);
        if (!partnerId.HasValue)
        {
            return Array.Empty<LeadDto>();
        }

        var leads = await PartnerApi.GetPartnerLeadsAsync(partnerId.Value, cancellationToken) ?? [];
        return leads
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new LeadDto(
                x.Id,
                x.Owner ?? "Lead sin dueño",
                x.Message ?? x.InternalNote ?? "Lead sin detalle adicional.",
                x.Status ?? "new",
                x.CreatedAt))
            .ToList();
    }
}
