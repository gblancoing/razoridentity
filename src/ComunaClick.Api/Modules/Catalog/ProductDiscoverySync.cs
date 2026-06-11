using ComunaClick.Api.Persistence;
using ComunaClick.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ComunaClick.Api.Modules.Catalog;

internal static class ProductDiscoverySync
{
    public static async Task EnrichDiscoveryIdsAsync(
        CoreDbContext db,
        IReadOnlyList<Product> products,
        CancellationToken cancellationToken = default)
    {
        if (products.Count == 0)
        {
            return;
        }

        var productIds = products.Select(x => x.Id).ToList();
        var rows = await db.ProductDiscoverySubcategories.AsNoTracking()
            .Where(x => productIds.Contains(x.ProductId))
            .ToListAsync(cancellationToken);

        var byProduct = rows.GroupBy(x => x.ProductId).ToDictionary(g => g.Key, g => (IReadOnlyList<Guid>)g.Select(x => x.SubcategoryId).ToList());

        foreach (var product in products)
        {
            product.DiscoverySubcategoryIds = byProduct.TryGetValue(product.Id, out var ids)
                ? ids
                : Array.Empty<Guid>();
        }
    }

    public static async Task SyncSubcategoriesAsync(
        CoreDbContext db,
        Guid productId,
        IReadOnlyList<Guid>? subcategoryIds,
        CancellationToken cancellationToken = default)
    {
        var existing = await db.ProductDiscoverySubcategories
            .Where(x => x.ProductId == productId)
            .ToListAsync(cancellationToken);

        db.ProductDiscoverySubcategories.RemoveRange(existing);

        if (subcategoryIds is not { Count: > 0 })
        {
            return;
        }

        var distinct = subcategoryIds.Where(x => x != Guid.Empty).Distinct().ToList();
        if (distinct.Count == 0)
        {
            return;
        }

        var validIds = await db.ProductSubcategories.AsNoTracking()
            .Where(x => x.IsActive && distinct.Contains(x.Id))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        foreach (var subcategoryId in validIds)
        {
            db.ProductDiscoverySubcategories.Add(new ProductDiscoverySubcategory
            {
                ProductId = productId,
                SubcategoryId = subcategoryId,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }
    }
}
