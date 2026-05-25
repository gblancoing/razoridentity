using System.Net.Http;
using ComunaClick.Shared.Api.Partner;

namespace ComunaClick.SharedUI.Services.Partner;

internal static class PartnerApiServiceHelper
{
    public static async Task<Guid?> ResolvePartnerIdAsync(
        AuthStateService authState,
        PartnerApiClient partnerApi,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var mine = await partnerApi.ListMyPartnersAsync(cancellationToken);
            if (mine is null || mine.Count == 0)
            {
                return null;
            }

            if (authState.PartnerId is { } tokenPartner && tokenPartner != Guid.Empty)
            {
                var active = mine.FirstOrDefault(p => p.Id == tokenPartner);
                if (active is not null)
                {
                    return active.Id;
                }
            }

            return mine[0].Id;
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }
}
