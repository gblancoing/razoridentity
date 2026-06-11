using ComunaClick.Api.Persistence;
using ComunaClick.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ComunaClick.Api.Modules.Catalog;

internal static class ProductMediaSync
{
    public static async Task EnrichAsync(CoreDbContext db, Product product, CancellationToken cancellationToken)
    {
        var images = await db.ProductImages.AsNoTracking()
            .Where(x => x.ProductId == product.Id)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        product.ImageUrls = images.Select(x => x.Url).ToList();
        product.Images = images
            .Select(x => new ProductImageSnapshot { Id = x.Id, Url = x.Url, SortOrder = x.SortOrder })
            .ToList();

        if (string.IsNullOrWhiteSpace(product.ImageUrl) && product.ImageUrls.Count > 0)
        {
            product.ImageUrl = product.ImageUrls[0];
        }

        if (product.PartnerCatalogCategoryId.HasValue)
        {
            product.CatalogCategoryName = await db.PartnerCatalogCategories.AsNoTracking()
                .Where(x => x.Id == product.PartnerCatalogCategoryId.Value)
                .Select(x => x.Name)
                .FirstOrDefaultAsync(cancellationToken);
        }
    }

    public static async Task EnrichManyAsync(CoreDbContext db, IList<Product> products, CancellationToken cancellationToken)
    {
        if (products.Count == 0)
        {
            return;
        }

        var ids = products.Select(x => x.Id).ToList();
        var images = await db.ProductImages.AsNoTracking()
            .Where(x => ids.Contains(x.ProductId))
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        var categoryIds = products
            .Where(x => x.PartnerCatalogCategoryId.HasValue)
            .Select(x => x.PartnerCatalogCategoryId!.Value)
            .Distinct()
            .ToList();

        var categoryNames = categoryIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.PartnerCatalogCategories.AsNoTracking()
                .Where(x => categoryIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);

        var byProduct = images.GroupBy(x => x.ProductId).ToDictionary(g => g.Key, g => g.ToList());
        foreach (var product in products)
        {
            if (!byProduct.TryGetValue(product.Id, out var list))
            {
                product.ImageUrls = Array.Empty<string>();
                product.Images = Array.Empty<ProductImageSnapshot>();
            }
            else
            {
                product.ImageUrls = list.Select(x => x.Url).ToList();
                product.Images = list
                    .Select(x => new ProductImageSnapshot { Id = x.Id, Url = x.Url, SortOrder = x.SortOrder })
                    .ToList();
                if (string.IsNullOrWhiteSpace(product.ImageUrl) && product.ImageUrls.Count > 0)
                {
                    product.ImageUrl = product.ImageUrls[0];
                }
            }

            if (product.PartnerCatalogCategoryId is Guid catId && categoryNames.TryGetValue(catId, out var name))
            {
                product.CatalogCategoryName = name;
                if (string.IsNullOrWhiteSpace(product.Category))
                {
                    product.Category = name;
                }
            }
        }
    }

    public static async Task SyncPrimaryImageUrlAsync(CoreDbContext db, Guid productId, CancellationToken cancellationToken)
    {
        var product = await db.Products.FirstOrDefaultAsync(x => x.Id == productId, cancellationToken);
        if (product is null)
        {
            return;
        }

        var first = await db.ProductImages.AsNoTracking()
            .Where(x => x.ProductId == productId)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.CreatedAt)
            .Select(x => x.Url)
            .FirstOrDefaultAsync(cancellationToken);

        product.ImageUrl = first;
        product.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }
}
