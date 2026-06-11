using ComunaClick.Api.Configuration;
using ComunaClick.Api.Persistence;
using ComunaClick.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ComunaClick.Api.Modules.Catalog;

public sealed class ProductInventoryService : IProductInventoryService
{
    private readonly CoreDbContext _db;
    private readonly IStockNotificationService _stockNotifications;
    private readonly InventoryOptions _options;

    public ProductInventoryService(
        CoreDbContext db,
        IStockNotificationService stockNotifications,
        IOptions<InventoryOptions> options)
    {
        _db = db;
        _stockNotifications = stockNotifications;
        _options = options.Value;
    }

    public async Task<int> GetAvailableQuantityAsync(Guid productId, Guid? excludeOrderId = null, CancellationToken cancellationToken = default)
    {
        var map = await GetAvailableQuantitiesAsync([productId], excludeOrderId, cancellationToken);
        return map.TryGetValue(productId, out var available) ? available : 0;
    }

    public async Task<IReadOnlyDictionary<Guid, int>> GetAvailableQuantitiesAsync(
        IReadOnlyCollection<Guid> productIds,
        Guid? excludeOrderId = null,
        CancellationToken cancellationToken = default)
    {
        if (productIds.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        var distinctIds = productIds.Distinct().ToList();
        var onHand = await _db.ProductInventories.AsNoTracking()
            .Where(x => distinctIds.Contains(x.ProductId))
            .ToDictionaryAsync(x => x.ProductId, x => x.Quantity, cancellationToken);

        var reserved = await GetReservedQuantitiesAsync(distinctIds, excludeOrderId, cancellationToken);

        var result = new Dictionary<Guid, int>(distinctIds.Count);
        foreach (var productId in distinctIds)
        {
            var physical = onHand.TryGetValue(productId, out var quantity) ? quantity : 0;
            var held = reserved.TryGetValue(productId, out var reservedQty) ? reservedQty : 0;
            result[productId] = Math.Max(0, physical - held);
        }

        return result;
    }

    public async Task<(bool Ok, string? Message)> ValidateLineItemsAsync(
        IReadOnlyCollection<(Guid ProductId, int Quantity)> lines,
        Guid? excludeOrderId = null,
        CancellationToken cancellationToken = default)
    {
        if (lines.Count == 0)
        {
            return (false, "El pedido debe incluir al menos un producto.");
        }

        var grouped = lines
            .GroupBy(x => x.ProductId)
            .Select(g => (ProductId: g.Key, Quantity: g.Sum(x => x.Quantity)))
            .ToList();

        var productIds = grouped.Select(x => x.ProductId).ToList();
        var products = await _db.Products.AsNoTracking()
            .Where(x => productIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);

        if (products.Count != productIds.Count)
        {
            return (false, "Uno o más productos no están disponibles.");
        }

        var available = await GetAvailableQuantitiesAsync(productIds, excludeOrderId, cancellationToken);
        foreach (var line in grouped)
        {
            if (!available.TryGetValue(line.ProductId, out var availableQty) || line.Quantity > availableQty)
            {
                var name = products.TryGetValue(line.ProductId, out var productName) ? productName : "Producto";
                if (availableQty <= 0)
                {
                    return (false, $"\"{name}\" no tiene stock disponible.");
                }

                return (false, $"Solo hay {availableQty} unidad(es) disponibles de \"{name}\".");
            }
        }

        return (true, null);
    }

    public async Task FulfillOrderAsync(Order order, CancellationToken cancellationToken = default)
    {
        if (order.Items.Count == 0)
        {
            return;
        }

        var productIds = order.Items.Select(x => x.ProductId).Distinct().ToList();
        var products = await _db.Products
            .Where(x => productIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        foreach (var item in order.Items)
        {
            if (!products.TryGetValue(item.ProductId, out var product))
            {
                continue;
            }

            var inventory = await _db.ProductInventories.FirstOrDefaultAsync(x => x.ProductId == item.ProductId, cancellationToken);
            if (inventory is null)
            {
                inventory = new ProductInventory
                {
                    ProductId = item.ProductId,
                    Quantity = 0,
                    UpdatedAt = DateTimeOffset.UtcNow
                };
                _db.ProductInventories.Add(inventory);
            }

            var previous = inventory.Quantity;
            inventory.Quantity = Math.Max(0, inventory.Quantity - item.Quantity);
            inventory.UpdatedAt = DateTimeOffset.UtcNow;

            await NotifyStockLevelsAsync(product, previous, inventory.Quantity, "order_paid", order.Id, cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task EnsureInventoryRowAsync(Guid productId, int initialQuantity, CancellationToken cancellationToken = default)
    {
        var exists = await _db.ProductInventories.AnyAsync(x => x.ProductId == productId, cancellationToken);
        if (exists)
        {
            return;
        }

        _db.ProductInventories.Add(new ProductInventory
        {
            ProductId = productId,
            Quantity = Math.Max(0, initialQuantity),
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken);
    }

    public Task NotifyStockLevelsAsync(
        Product product,
        int previousOnHand,
        int newOnHand,
        string reason,
        Guid? referenceId = null,
        CancellationToken cancellationToken = default)
        => _stockNotifications.NotifyQuantityChangedAsync(
            product,
            previousOnHand,
            newOnHand,
            reason,
            referenceId,
            cancellationToken);

    private async Task<Dictionary<Guid, int>> GetReservedQuantitiesAsync(
        IReadOnlyCollection<Guid> productIds,
        Guid? excludeOrderId,
        CancellationToken cancellationToken)
    {
        var reservingStatuses = _options.ReservingOrderStatuses
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .ToList();

        if (reservingStatuses.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        var query =
            from item in _db.OrderItems.AsNoTracking()
            join order in _db.Orders.AsNoTracking() on item.OrderId equals order.Id
            where productIds.Contains(item.ProductId)
                  && reservingStatuses.Contains(order.Status)
                  && (!excludeOrderId.HasValue || order.Id != excludeOrderId.Value)
            group item by item.ProductId
            into grouped
            select new { ProductId = grouped.Key, Quantity = grouped.Sum(x => x.Quantity) };

        return await query.ToDictionaryAsync(x => x.ProductId, x => x.Quantity, cancellationToken);
    }
}
