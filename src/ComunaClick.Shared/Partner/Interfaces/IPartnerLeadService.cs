using ComunaClick.Shared.Partner.Leads;

namespace ComunaClick.Shared.Partner.Interfaces;

public interface IPartnerLeadService
{
    Task<IReadOnlyList<LeadDto>> GetLeadsAsync(CancellationToken cancellationToken = default);
}
