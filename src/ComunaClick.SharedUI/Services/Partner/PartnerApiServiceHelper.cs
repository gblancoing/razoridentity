using System.Net.Http;
using ComunaClick.Shared.Api.Partner;

namespace ComunaClick.SharedUI.Services.Partner;

internal static class PartnerApiServiceHelper
{
    public static async Task<Guid?> ResolvePartnerIdAsync(
        AuthStateService authState,
        PartnerApiClient partnerApi,
        CancellationToken cancellationToken)
    {
        if (authState.PartnerId is { } p && p != Guid.Empty)
            return p;

        try
        {
            var mine = await partnerApi.ListMyPartnersAsync(cancellationToken);
            return mine?.FirstOrDefault()?.Id;
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }
}
