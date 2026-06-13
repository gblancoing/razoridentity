using ComunaClick.Api.Persistence;
using ComunaClick.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ComunaClick.Api.Modules.Catalog;

public static class OrderInventoryFulfillment
{
    private static readonly HashSet<string> PaidStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "paid",
        "approved"
    };

    public static bool IsPaidStatus(string? status)
        => !string.IsNullOrWhiteSpace(status) && PaidStatuses.Contains(status.Trim());

    public static async Task TryFulfillPaidOrderAsync(
        CoreDbContext db,
        IProductInventoryService inventoryService,
        Guid orderId,
        string newStatus,
        CancellationToken cancellationToken = default)
    {
        if (!IsPaidStatus(newStatus))
        {
            return;
        }

        var order = await db.Orders
            .IgnoreQueryFilters()
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == orderId, cancellationToken);

        if (order is null)
        {
            return;
        }

        await TryFulfillPaidOrderAsync(db, inventoryService, order, newStatus, cancellationToken);
    }

    public static async Task TryFulfillPaidOrderAsync(
        CoreDbContext db,
        IProductInventoryService inventoryService,
        Order order,
        string newStatus,
        CancellationToken cancellationToken = default)
    {
        if (!IsPaidStatus(newStatus) || order.Items.Count == 0)
        {
            return;
        }

        // Idempotente por diseño: el descuento de stock para una orden ocurre una sola vez
        // en su vida (flag inventory_fulfilled_at), sin depender del estado anterior inmediato.
        if (order.InventoryFulfilledAt is not null)
        {
            return;
        }

        await inventoryService.FulfillOrderAsync(order, cancellationToken);
    }
}
