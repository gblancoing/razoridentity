using ComunaClick.Shared.Api.Partner;
using ComunaClick.SharedUI.Services;

namespace ComunaClick.SharedUI.Services.Partner;

public abstract class PartnerServiceBase
{
    protected readonly PartnerApiClient PartnerApi;
    protected readonly AuthStateService AuthState;

    protected PartnerServiceBase(PartnerApiClient partnerApi, AuthStateService authState)
    {
        PartnerApi = partnerApi;
        AuthState = authState;
    }

    protected async Task<Guid?> ResolvePartnerIdAsync(CancellationToken cancellationToken)
    {
        if (!AuthState.IsAuthenticated)
        {
            await AuthState.InitializeAsync(cancellationToken);
        }

        return AuthState.PartnerId is Guid partnerId && partnerId != Guid.Empty
            ? partnerId
            : null;
    }
}
