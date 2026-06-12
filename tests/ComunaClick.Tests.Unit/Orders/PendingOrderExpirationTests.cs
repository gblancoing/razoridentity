using ComunaClick.Api.Configuration;
using ComunaClick.Api.Jobs;
using ComunaClick.Api.Modules.Catalog;
using ComunaClick.Api.Modules.Customers;
using ComunaClick.Api.Modules.Orders;
using ComunaClick.Api.Modules.Orders.Contracts;
using ComunaClick.Api.Persistence;
using ComunaClick.Tests.Unit.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace ComunaClick.Tests.Unit.Orders;

public sealed class PendingOrderExpirationTests
{
    private static InventoryOptions ReservingOptions(int ttlMinutes = 5) => new()
    {
        ReservingOrderStatuses = ["payment_pending"],
        PendingOrderTtlMinutes = ttlMinutes
    };

    private static OrderCheckoutService CreateCheckoutService(CoreDbContext db, InventoryOptions options)
        => new(
            db,
            new RecordingOrderNotificationService(),
            TestDb.CreateInventoryService(db, options),
            new GuestCustomerService(db),
            TestDb.CreateDeliveryPricingService());

    private static PendingOrderExpirationJob CreateJob(CoreDbContext db, InventoryOptions options)
        => new(db, Options.Create(options), NullLogger<PendingOrderExpirationJob>.Instance);

    private static async Task<ComunaClick.Api.Persistence.Entities.Order> CreatePendingOrderAsync(
        CoreDbContext db,
        OrderCheckoutService service,
        Guid tenantId,
        Guid partnerId,
        Guid customerId,
        Guid productId,
        int quantity = 1)
    {
        var result = await service.CreateOrderAsync(
            tenantId,
            customerId,
            new OrderCreateRequest(
                partnerId,
                customerId,
                DeliveryFee: 0m,
                Currency: "CLP",
                Items: [new OrderItemCreateRequest(productId, quantity, 1000m)]));

        Assert.True(result.Success, result.ErrorMessage);
        return result.Order!;
    }

    // Con la reserva encendida, una orden pendiente fresca retiene el stock:
    // un segundo comprador no puede pasar la validación por la última unidad.
    [Fact]
    public async Task FreshPendingOrder_ReservesStock_AndBlocksSecondPurchase()
    {
        await using var db = TestDb.Create();
        var (tenantId, partner, customer, product) = TestDb.SeedCatalog(db, stock: 1);
        var options = ReservingOptions();
        var service = CreateCheckoutService(db, options);

        await CreatePendingOrderAsync(db, service, tenantId, partner.Id, customer.Id, product.Id);

        var second = await service.CreateOrderAsync(
            tenantId,
            customer.Id,
            new OrderCreateRequest(
                partner.Id,
                customer.Id,
                DeliveryFee: 0m,
                Currency: "CLP",
                Items: [new OrderItemCreateRequest(product.Id, 1, 1000m)]));

        Assert.False(second.Success);
    }

    // La reserva vencida se ignora EN TIEMPO REAL: aunque el job todavía no
    // canceló la orden, el stock vuelve a estar disponible para otro comprador.
    [Fact]
    public async Task ExpiredPendingOrder_ReleasesStock_InRealTime_WithoutJob()
    {
        await using var db = TestDb.Create();
        var (tenantId, partner, customer, product) = TestDb.SeedCatalog(db, stock: 1);
        var options = ReservingOptions(ttlMinutes: 5);
        var service = CreateCheckoutService(db, options);

        var stale = await CreatePendingOrderAsync(db, service, tenantId, partner.Id, customer.Id, product.Id);
        stale.CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10);
        await db.SaveChangesAsync();

        var second = await service.CreateOrderAsync(
            tenantId,
            customer.Id,
            new OrderCreateRequest(
                partner.Id,
                customer.Id,
                DeliveryFee: 0m,
                Currency: "CLP",
                Items: [new OrderItemCreateRequest(product.Id, 1, 1000m)]));

        Assert.True(second.Success);
        // La orden vencida sigue payment_pending hasta que el job la cancele.
        Assert.Equal(OrderStatusMachine.PaymentPending, stale.Status);
    }

    // TTL = 0 desactiva la expiración: la reserva pendiente no vence nunca.
    [Fact]
    public async Task TtlZero_DisablesExpiration()
    {
        await using var db = TestDb.Create();
        var (tenantId, partner, customer, product) = TestDb.SeedCatalog(db, stock: 1);
        var options = ReservingOptions(ttlMinutes: 0);
        var service = CreateCheckoutService(db, options);

        var stale = await CreatePendingOrderAsync(db, service, tenantId, partner.Id, customer.Id, product.Id);
        stale.CreatedAt = DateTimeOffset.UtcNow.AddDays(-1);
        await db.SaveChangesAsync();

        var second = await service.CreateOrderAsync(
            tenantId,
            customer.Id,
            new OrderCreateRequest(
                partner.Id,
                customer.Id,
                DeliveryFee: 0m,
                Currency: "CLP",
                Items: [new OrderItemCreateRequest(product.Id, 1, 1000m)]));

        Assert.False(second.Success);
        Assert.Equal(0, await CreateJob(db, options).RunAsync());
    }

    [Fact]
    public async Task Job_CancelsExpiredPending_LeavesFreshAndPaid_AndIsIdempotent()
    {
        await using var db = TestDb.Create();
        var (tenantId, partner, customer, product) = TestDb.SeedCatalog(db, stock: 10);
        var options = ReservingOptions(ttlMinutes: 5);
        var service = CreateCheckoutService(db, options);

        var expired = await CreatePendingOrderAsync(db, service, tenantId, partner.Id, customer.Id, product.Id);
        expired.CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10);

        var fresh = await CreatePendingOrderAsync(db, service, tenantId, partner.Id, customer.Id, product.Id);

        var paidButOld = await CreatePendingOrderAsync(db, service, tenantId, partner.Id, customer.Id, product.Id);
        paidButOld.CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-20);
        paidButOld.Status = OrderStatusMachine.Paid;
        await db.SaveChangesAsync();

        var job = CreateJob(db, options);

        Assert.Equal(1, await job.RunAsync());
        Assert.Equal(OrderStatusMachine.Cancelled, expired.Status);
        Assert.Equal(OrderStatusMachine.PaymentPending, fresh.Status);
        Assert.Equal(OrderStatusMachine.Paid, paidButOld.Status);

        // Idempotente: una segunda pasada no encuentra nada que cancelar.
        Assert.Equal(0, await job.RunAsync());
    }

    // Caso borde pago-vs-expiración: el job ya canceló la orden, pero el pago
    // confirmado llega igual (webhook MP). El pago GANA: el estado vuelve a
    // paid y el descuento de stock ocurre exactamente una vez (idempotente).
    [Fact]
    public async Task PaymentArrivingAfterExpiration_Wins_AndFulfillsStockExactlyOnce()
    {
        await using var db = TestDb.Create();
        var (tenantId, partner, customer, product) = TestDb.SeedCatalog(db, stock: 3);
        var options = ReservingOptions(ttlMinutes: 5);
        var service = CreateCheckoutService(db, options);
        var inventoryService = TestDb.CreateInventoryService(db, options);

        var order = await CreatePendingOrderAsync(db, service, tenantId, partner.Id, customer.Id, product.Id, quantity: 2);
        order.CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10);
        await db.SaveChangesAsync();

        Assert.Equal(1, await CreateJob(db, options).RunAsync());
        Assert.Equal(OrderStatusMachine.Cancelled, order.Status);

        // Mismo flujo que MercadoPagoWebhookService al recibir el pago aprobado:
        // repone paid sin pasar por la máquina de estados y luego descuenta.
        if (!OrderInventoryFulfillment.IsPaidStatus(order.Status))
        {
            order.Status = OrderStatusMachine.Paid;
            order.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
        }

        await OrderInventoryFulfillment.TryFulfillPaidOrderAsync(db, inventoryService, order, OrderStatusMachine.Paid);
        // Reintento del webhook (MP puede notificar varias veces): no descuenta de nuevo.
        await OrderInventoryFulfillment.TryFulfillPaidOrderAsync(db, inventoryService, order, OrderStatusMachine.Paid);

        Assert.Equal(OrderStatusMachine.Paid, order.Status);
        Assert.NotNull(order.InventoryFulfilledAt);
        var onHand = await db.ProductInventories.AsNoTracking().FirstAsync(x => x.ProductId == product.Id);
        Assert.Equal(1, onHand.Quantity); // 3 - 2, una sola vez
    }

    // Carrito multi-negocio: cada sub-orden expira de forma independiente según
    // su propio CreatedAt (son órdenes separadas, incluso de tenants distintos).
    [Fact]
    public async Task MultiPartnerCartOrders_ExpireIndependently()
    {
        await using var db = TestDb.Create();
        var (tenantA, partnerA, customerA, productA) = TestDb.SeedCatalog(db, stock: 5);
        var (tenantB, partnerB, customerB, productB) = TestDb.SeedCatalog(db, stock: 5);
        var options = ReservingOptions(ttlMinutes: 5);
        var service = CreateCheckoutService(db, options);

        var orderA = await CreatePendingOrderAsync(db, service, tenantA, partnerA.Id, customerA.Id, productA.Id);
        orderA.CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10);
        await db.SaveChangesAsync();

        var orderB = await CreatePendingOrderAsync(db, service, tenantB, partnerB.Id, customerB.Id, productB.Id);

        Assert.Equal(1, await CreateJob(db, options).RunAsync());
        Assert.Equal(OrderStatusMachine.Cancelled, orderA.Status);
        Assert.Equal(OrderStatusMachine.PaymentPending, orderB.Status);
    }
}
