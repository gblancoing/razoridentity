using System.Net.Http;
using ComunaClick.Shared.Api.Partner;
using ComunaClick.Shared.Partner.Catalog;
using ComunaClick.Shared.Partner.Interfaces;

namespace ComunaClick.SharedUI.Services.Partner;

public sealed class PartnerApiPartnerCatalogService : IPartnerCatalogService
{
    private readonly PartnerApiClient _partnerApi;
    private readonly AuthStateService _authState;

    public PartnerApiPartnerCatalogService(PartnerApiClient partnerApi, AuthStateService authState)
    {
        _partnerApi = partnerApi;
        _authState = authState;
    }

    public async Task<IReadOnlyList<CatalogItemDto>> GetCatalogAsync(CancellationToken cancellationToken = default)
    {
        var partnerId = await PartnerApiServiceHelper.ResolvePartnerIdAsync(_authState, _partnerApi, cancellationToken);
        if (!partnerId.HasValue)
            return Array.Empty<CatalogItemDto>();

        try
        {
            var products = await _partnerApi.GetPartnerProductsAsync(partnerId.Value, cancellationToken) ?? Array.Empty<Product>();
            var services = await _partnerApi.GetPartnerServicesAsync(partnerId.Value, cancellationToken) ?? Array.Empty<Service>();
            var professionals = await _partnerApi.GetPartnerProfessionalsAsync(partnerId.Value, cancellationToken) ?? Array.Empty<Professional>();

            var list = new List<CatalogItemDto>();

            foreach (var x in products)
            {
                list.Add(new CatalogItemDto(
                    x.Id,
                    x.Name ?? "Producto",
                    string.IsNullOrWhiteSpace(x.Description) ? (x.Category ?? "") : x.Description!,
                    "Product",
                    (decimal)x.Price,
                    x.IsActive));
            }

            foreach (var x in services)
            {
                list.Add(new CatalogItemDto(
                    x.Id,
                    x.Name ?? "Servicio",
                    string.IsNullOrWhiteSpace(x.Description) ? (x.Category ?? "") : x.Description!,
                    "Service",
                    (decimal)x.Price,
                    x.IsActive));
            }

            foreach (var x in professionals)
            {
                list.Add(new CatalogItemDto(
                    x.Id,
                    x.Name ?? "Profesional",
                    string.IsNullOrWhiteSpace(x.Specialty) ? (x.Bio ?? "") : x.Specialty!,
                    "Professional",
                    0m,
                    x.IsActive));
            }

            return list;
        }
        catch (HttpRequestException)
        {
            return Array.Empty<CatalogItemDto>();
        }
    }
}
