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
        string? previousStatus,
        string newStatus,
        CancellationToken cancellationToken = default)
    {
        if (!IsPaidStatus(newStatus) || IsPaidStatus(previousStatus))
        {
            return;
        }

        var order = await db.Orders
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == orderId, cancellationToken);

        if (order is null || order.Items.Count == 0)
        {
            return;
        }

        await inventoryService.FulfillOrderAsync(order, cancellationToken);
    }

    public static async Task TryFulfillPaidOrderAsync(
        CoreDbContext db,
        IProductInventoryService inventoryService,
        Order order,
        string? previousStatus,
        string newStatus,
        CancellationToken cancellationToken = default)
    {
        if (!IsPaidStatus(newStatus) || IsPaidStatus(previousStatus) || order.Items.Count == 0)
        {
            return;
        }

        await inventoryService.FulfillOrderAsync(order, cancellationToken);
    }
}
