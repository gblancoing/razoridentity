using ComunaClick.Shared.Partner.Catalog;

namespace ComunaClick.Shared.Partner.Interfaces;

public interface IPartnerCatalogService
{
    Task<IReadOnlyList<CatalogItemDto>> GetCatalogAsync(CancellationToken cancellationToken = default);
}
