using ComunaClick.Api.Persistence.Entities;

namespace ComunaClick.Api.Modules.Catalog;

internal static class ProductResponses
{
    public static object ToPartnerResponse(Product product, bool includeCost, int? availableQuantity = null)
    {
        var onHand = ProductInventoryRules.GetOnHandQuantity(product.Inventory);
        var available = availableQuantity ?? onHand;
        var images = product.Images.Select(x => new { x.Id, x.Url, x.SortOrder }).ToList();
        return new
        {
            product.Id,
            product.TenantId,
            product.PartnerId,
            product.Name,
            product.Description,
            product.Category,
            product.PartnerCatalogCategoryId,
            CatalogCategoryName = product.CatalogCategoryName,
            DiscoverySubcategoryIds = product.DiscoverySubcategoryIds,
            product.ProductAddress,
            product.CountryId,
            product.RegionId,
            product.ComunaId,
            product.Latitude,
            product.Longitude,
            product.ImageUrl,
            ImageUrls = product.ImageUrls,
            Images = images,
            Price = product.Price,
            CostPrice = includeCost ? product.CostPrice : null,
            product.Currency,
            product.IsActive,
            product.CreatedAt,
            product.UpdatedAt,
            InStock = ProductInventoryRules.IsInStock(available),
            StockQuantity = onHand,
            AvailableQuantity = available,
            ReservedQuantity = Math.Max(0, onHand - available),
            Inventory = product.Inventory is null ? null : new
            {
                product.Inventory.ProductId,
                product.Inventory.Quantity,
                product.Inventory.UpdatedAt
            }
        };
    }

    public static object ToPublicResponse(
        Product product,
        string? partnerName = null,
        string? partnerLogoUrl = null,
        int? availableQuantity = null)
    {
        var onHand = ProductInventoryRules.GetOnHandQuantity(product.Inventory);
        var available = availableQuantity ?? onHand;

        return new
        {
            product.Id,
            product.TenantId,
            product.PartnerId,
            PartnerName = partnerName,
            PartnerLogoUrl = partnerLogoUrl,
            product.Name,
            product.Description,
            product.Category,
            product.PartnerCatalogCategoryId,
            CatalogCategoryName = product.CatalogCategoryName,
            DiscoverySubcategoryIds = product.DiscoverySubcategoryIds,
            product.ProductAddress,
            product.CountryId,
            product.RegionId,
            product.ComunaId,
            product.Latitude,
            product.Longitude,
            product.ImageUrl,
            ImageUrls = product.ImageUrls,
            Price = product.Price,
            product.Currency,
            product.IsActive,
            product.CreatedAt,
            product.UpdatedAt,
            InStock = ProductInventoryRules.IsInStock(available),
            StockQuantity = onHand,
            AvailableQuantity = available,
            Inventory = product.Inventory is null ? null : new
            {
                product.Inventory.ProductId,
                product.Inventory.Quantity,
                product.Inventory.UpdatedAt
            }
        };
    }
}
