using ComunaClick.Api.Modules.Catalog;
using ComunaClick.Api.Persistence.Entities;
using ComunaClick.Tests.Unit.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ComunaClick.Tests.Unit.Catalog;

public sealed class OrderInventoryFulfillmentTests
{
    [Fact]
    public async Task TryFulfillPaidOrder_CalledTwice_DecrementsStockOnlyOnce()
    {
        await using var db = TestDb.Create();
        var (tenantId, partner, customer, product) = TestDb.SeedCatalog(db, stock: 5);
        var order = SeedPaidOrder(db, tenantId, partner.Id, customer.Id, product.Id, quantity: 2);
        var inventoryService = TestDb.CreateInventoryService(db);

        await OrderInventoryFulfillment.TryFulfillPaidOrderAsync(db, inventoryService, order, "paid");
        await OrderInventoryFulfillment.TryFulfillPaidOrderAsync(db, inventoryService, order, "paid");

        var inventory = await db.ProductInventories.AsNoTracking().SingleAsync(x => x.ProductId == product.Id);
        Assert.Equal(3, inventory.Quantity);
        Assert.NotNull(order.InventoryFulfilledAt);
    }

    [Fact]
    public async Task TryFulfillPaidOrder_AcrossDifferentContexts_DecrementsStockOnlyOnce()
    {
        var dbName = $"fulfill-{Guid.NewGuid():N}";
        await using var db1 = TestDb.Create(dbName);
        var (tenantId, partner, customer, product) = TestDb.SeedCatalog(db1, stock: 5);
        var order = SeedPaidOrder(db1, tenantId, partner.Id, customer.Id, product.Id, quantity: 2);

        await OrderInventoryFulfillment.TryFulfillPaidOrderAsync(db1, TestDb.CreateInventoryService(db1), order, "paid");

        // Simula que otro request (otro DbContext) reprocesa el mismo pago.
        await using var db2 = TestDb.Create(dbName);
        var orderFromOtherContext = await db2.Orders.Include(x => x.Items).SingleAsync(x => x.Id == order.Id);
        orderFromOtherContext.InventoryFulfilledAt = null; // estado en memoria desactualizado
        await OrderInventoryFulfillment.TryFulfillPaidOrderAsync(db2, TestDb.CreateInventoryService(db2), orderFromOtherContext, "paid");

        await using var verify = TestDb.Create(dbName);
        var inventory = await verify.ProductInventories.AsNoTracking().SingleAsync(x => x.ProductId == product.Id);
        Assert.Equal(3, inventory.Quantity);
    }

    [Fact]
    public async Task TryFulfillPaidOrder_NonPaidStatus_DoesNothing()
    {
        await using var db = TestDb.Create();
        var (tenantId, partner, customer, product) = TestDb.SeedCatalog(db, stock: 5);
        var order = SeedPaidOrder(db, tenantId, partner.Id, customer.Id, product.Id, quantity: 2);

        await OrderInventoryFulfillment.TryFulfillPaidOrderAsync(db, TestDb.CreateInventoryService(db), order, "processing");

        var inventory = await db.ProductInventories.AsNoTracking().SingleAsync(x => x.ProductId == product.Id);
        Assert.Equal(5, inventory.Quantity);
        Assert.Null(order.InventoryFulfilledAt);
    }

    [Fact]
    public async Task RestoreOrder_AfterFulfillment_RestoresStock()
    {
        await using var db = TestDb.Create();
        var (tenantId, partner, customer, product) = TestDb.SeedCatalog(db, stock: 5);
        var order = SeedPaidOrder(db, tenantId, partner.Id, customer.Id, product.Id, quantity: 2);
        var inventoryService = TestDb.CreateInventoryService(db);

        await OrderInventoryFulfillment.TryFulfillPaidOrderAsync(db, inventoryService, order, "paid");
        await inventoryService.RestoreOrderAsync(order);

        var inventory = await db.ProductInventories.AsNoTracking().SingleAsync(x => x.ProductId == product.Id);
        Assert.Equal(5, inventory.Quantity);
        Assert.Null(order.InventoryFulfilledAt);
    }

    [Fact]
    public async Task RestoreOrder_WithoutPriorFulfillment_DoesNotInflateStock()
    {
        await using var db = TestDb.Create();
        var (tenantId, partner, customer, product) = TestDb.SeedCatalog(db, stock: 5);
        var order = SeedPaidOrder(db, tenantId, partner.Id, customer.Id, product.Id, quantity: 2);

        await TestDb.CreateInventoryService(db).RestoreOrderAsync(order);

        var inventory = await db.ProductInventories.AsNoTracking().SingleAsync(x => x.ProductId == product.Id);
        Assert.Equal(5, inventory.Quantity);
    }

    private static Order SeedPaidOrder(
        ComunaClick.Api.Persistence.CoreDbContext db,
        Guid tenantId,
        Guid partnerId,
        Guid customerId,
        Guid productId,
        int quantity)
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PartnerId = partnerId,
            CustomerId = customerId,
            Status = "paid",
            Currency = "CLP",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Items =
            [
                new OrderItem
                {
                    Id = Guid.NewGuid(),
                    ProductId = productId,
                    Quantity = quantity,
                    UnitPrice = 1000m,
                    TotalPrice = 1000m * quantity
                }
            ]
        };

        db.Orders.Add(order);
        db.SaveChanges();
        return order;
    }
}
