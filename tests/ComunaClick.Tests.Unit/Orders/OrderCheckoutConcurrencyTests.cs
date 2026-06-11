using ComunaClick.Api.Modules.Customers;
using ComunaClick.Api.Modules.Orders;
using ComunaClick.Api.Modules.Orders.Contracts;
using ComunaClick.Tests.Unit.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ComunaClick.Tests.Unit.Orders;

public sealed class OrderCheckoutConcurrencyTests
{
    [Fact]
    public async Task CreateOrderAsync_TwoParallelBuyersLastUnit_OnlyOneOrderIsCreated()
    {
        var dbName = $"checkout-race-{Guid.NewGuid():N}";
        await using var seedDb = TestDb.Create(dbName);
        var (tenantId, partner, customer, product) = TestDb.SeedCatalog(seedDb, stock: 1);

        var request = new OrderCreateRequest(
            partner.Id,
            customer.Id,
            DeliveryFee: 0,
            Currency: "CLP",
            Items: [new OrderItemCreateRequest(product.Id, 1, product.Price)]);

        // Cada request concurrente usa su propio DbContext, como en producción.
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = Enumerable.Range(0, 2).Select(async _ =>
        {
            await using var db = TestDb.Create(dbName);
            var service = new OrderCheckoutService(
                db,
                new RecordingOrderNotificationService(),
                TestDb.CreateInventoryService(db),
                new GuestCustomerService(db));
            await gate.Task;
            return await service.CreateOrderAsync(tenantId, customer.Id, request);
        }).ToList();

        gate.SetResult();
        var results = await Task.WhenAll(tasks);

        Assert.Equal(1, results.Count(x => x.Success));
        Assert.Equal(1, results.Count(x => !x.Success));

        await using var verify = TestDb.Create(dbName);
        Assert.Equal(1, await verify.Orders.CountAsync());

        var inventory = await verify.ProductInventories.AsNoTracking().SingleAsync(x => x.ProductId == product.Id);
        Assert.True(inventory.Quantity >= 0);
    }

    [Fact]
    public async Task CreateOrderAsync_GeneratesExternalReferenceInSingleSave()
    {
        await using var db = TestDb.Create();
        var (tenantId, partner, customer, product) = TestDb.SeedCatalog(db, stock: 3);

        var service = new OrderCheckoutService(
            db,
            new RecordingOrderNotificationService(),
            TestDb.CreateInventoryService(db),
            new GuestCustomerService(db));

        var result = await service.CreateOrderAsync(
            tenantId,
            customer.Id,
            new OrderCreateRequest(
                partner.Id,
                customer.Id,
                DeliveryFee: 0,
                Currency: "CLP",
                Items: [new OrderItemCreateRequest(product.Id, 1, product.Price)]));

        Assert.True(result.Success);
        Assert.NotNull(result.Order);
        Assert.Equal($"cc-order-{result.Order!.Id:N}", result.Order.ExternalReference);
        Assert.Equal(customer.Email, result.Order.BuyerEmail);
    }
}
