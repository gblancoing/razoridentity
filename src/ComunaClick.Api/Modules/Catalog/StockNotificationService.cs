using System.Text.Json;
using ComunaClick.Api.Configuration;
using ComunaClick.Api.Persistence;
using ComunaClick.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ComunaClick.Api.Modules.Catalog;

public sealed class StockNotificationService : IStockNotificationService
{
    public const string TypeOut = "stock_out";
    public const string TypeLow = "stock_low";

    private readonly CoreDbContext _db;
    private readonly InventoryOptions _options;

    public StockNotificationService(CoreDbContext db, IOptions<InventoryOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    public async Task NotifyQuantityChangedAsync(
        Product product,
        int previousOnHand,
        int newOnHand,
        string reason,
        Guid? referenceId = null,
        CancellationToken cancellationToken = default)
    {
        var threshold = Math.Max(0, _options.LowStockThreshold);
        string? notificationType = null;

        if (previousOnHand > 0 && newOnHand <= 0)
        {
            notificationType = TypeOut;
        }
        else if (previousOnHand > threshold && newOnHand > 0 && newOnHand <= threshold)
        {
            notificationType = TypeLow;
        }

        if (notificationType is null)
        {
            return;
        }

        var recentDuplicate = await _db.Interactions.AsNoTracking()
            .AnyAsync(x =>
                x.TenantId == product.TenantId
                && x.PartnerId == product.PartnerId
                && x.Type == notificationType
                && x.ReferenceId == product.Id
                && x.CreatedAt >= DateTimeOffset.UtcNow.AddHours(-6),
                cancellationToken);

        if (recentDuplicate)
        {
            return;
        }

        var customerId = await ResolveNotificationCustomerIdAsync(product.TenantId, cancellationToken);
        if (!customerId.HasValue)
        {
            return;
        }

        var message = notificationType == TypeOut
            ? $"El producto \"{product.Name}\" se quedó sin stock. Repón inventario en Catálogo."
            : $"El producto \"{product.Name}\" tiene stock bajo ({newOnHand} unidad/es). Considera reponer pronto.";

        var interaction = new Interaction
        {
            TenantId = product.TenantId,
            CustomerId = customerId.Value,
            PartnerId = product.PartnerId,
            Type = notificationType,
            ReferenceId = product.Id,
            Payload = JsonSerializer.Serialize(new
            {
                message,
                productId = product.Id,
                productName = product.Name,
                quantity = newOnHand,
                threshold,
                reason
            }),
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.Interactions.Add(interaction);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<Guid?> ResolveNotificationCustomerIdAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var customerId = await _db.Customers.AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.CreatedAt)
            .Select(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return customerId == Guid.Empty ? null : customerId;
    }
}
