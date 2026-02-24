using ComunaClick.Shared.Partner.Catalog;
using ComunaClick.Shared.Partner.Interfaces;

namespace ComunaClick.SharedUI.Services.Mocks;

public sealed class MockPartnerCatalogService : IPartnerCatalogService
{
    public Task<IReadOnlyList<CatalogItemDto>> GetCatalogAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<CatalogItemDto> items = new List<CatalogItemDto>
        {
            new(Guid.NewGuid(), "Pan masa madre", "Producto · Stock alto", "Product", 12500m, true),
            new(Guid.NewGuid(), "Clase de yoga", "Servicio · Agenda abierta", "Service", 8900m, true),
            new(Guid.NewGuid(), "Corte premium", "Servicio · Alta demanda", "Service", 19000m, true)
        };

        return Task.FromResult(items);
    }
}
