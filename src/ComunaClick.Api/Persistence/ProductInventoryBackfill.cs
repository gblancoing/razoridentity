using ComunaClick.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ComunaClick.Api.Persistence;

public static class ProductInventoryBackfill
{
    public static async Task EnsureAllProductsHaveInventoryAsync(CoreDbContext db, ILogger logger, CancellationToken cancellationToken = default)
    {
        var missingProductIds = await db.Products.AsNoTracking()
            .Where(p => !db.ProductInventories.Any(i => i.ProductId == p.Id))
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        if (missingProductIds.Count == 0)
        {
            return;
        }

        logger.LogInformation("Creating inventory rows for {Count} products without stock record.", missingProductIds.Count);

        foreach (var productId in missingProductIds)
        {
            db.ProductInventories.Add(new ProductInventory
            {
                ProductId = productId,
                Quantity = 0,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
