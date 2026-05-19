using ComunaClick.Shared.Partner.Catalog;
using ComunaClick.Shared.Partner.Interfaces;
using ComunaClick.SharedUI.Services;

namespace ComunaClick.SharedUI.Services.Partner;

public sealed class PartnerCatalogService : PartnerServiceBase, IPartnerCatalogService
{
    public PartnerCatalogService(ComunaClick.Shared.Api.Partner.PartnerApiClient partnerApi, AuthStateService authState)
        : base(partnerApi, authState)
    {
    }

    public async Task<IReadOnlyList<CatalogItemDto>> GetCatalogAsync(CancellationToken cancellationToken = default)
    {
        var partnerId = await ResolvePartnerIdAsync(cancellationToken);
        if (!partnerId.HasValue)
        {
            return Array.Empty<CatalogItemDto>();
        }

        var partner = await PartnerApi.GetPartnerAsync(partnerId.Value, cancellationToken);
        if (string.Equals(partner?.Type, "A", StringComparison.OrdinalIgnoreCase))
        {
            var products = await PartnerApi.GetPartnerProductsAsync(partnerId.Value, cancellationToken) ?? [];
            return products
                .Select(x => new CatalogItemDto(
                    x.Id,
                    x.Name ?? "Producto",
                    string.IsNullOrWhiteSpace(x.Description) ? "Producto sin descripción comercial." : x.Description,
                    "Product",
                    Convert.ToDecimal(x.Price),
                    x.IsActive))
                .ToList();
        }

        if (string.Equals(partner?.Type, "B", StringComparison.OrdinalIgnoreCase))
        {
            var services = await PartnerApi.GetPartnerServicesAsync(partnerId.Value, cancellationToken) ?? [];
            return services
                .Select(x => new CatalogItemDto(
                    x.Id,
                    x.Name ?? "Servicio",
                    string.IsNullOrWhiteSpace(x.Description) ? "Servicio sin descripción comercial." : x.Description,
                    "Service",
                    Convert.ToDecimal(x.Price),
                    x.IsActive))
                .ToList();
        }

        var professionals = await PartnerApi.GetPartnerProfessionalsAsync(partnerId.Value, cancellationToken) ?? [];
        return professionals
            .Select(x => new CatalogItemDto(
                x.Id,
                x.Name ?? "Profesional",
                string.IsNullOrWhiteSpace(x.Bio) ? (x.Specialty ?? "Perfil profesional") : x.Bio,
                "Professional",
                0m,
                x.IsActive))
            .ToList();
    }
}
