using ComunaClick.Api.Modules.Customers;
using ComunaClick.Api.Modules.Orders;
using ComunaClick.Api.Persistence;
using ComunaClick.Api.Persistence.Entities;
using ComunaClick.Tests.Unit.TestSupport;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Xunit;

namespace ComunaClick.Tests.Unit.Orders;

public sealed class OrdersPublicAccessTests
{
    // H9: con token válido se accede a la orden pública.
    [Fact]
    public async Task GetPublic_WithValidToken_ReturnsOrder()
    {
        await using var db = TestDb.Create();
        var (order, tokens) = SeedOrder(db);
        var controller = CreateController(db, tokens, allowLegacy: false);

        var token = tokens.Create(order.Id, order.CustomerId);
        var result = await controller.GetPublic(order.Id, customerId: null, token: token);

        Assert.IsType<OkObjectResult>(result.Result);
    }

    // H9: sin token y con legacy deshabilitado, el par id+customerId no da acceso.
    [Fact]
    public async Task GetPublic_LegacyDisabled_WithoutToken_ReturnsNotFound()
    {
        await using var db = TestDb.Create();
        var (order, tokens) = SeedOrder(db);
        var controller = CreateController(db, tokens, allowLegacy: false);

        var result = await controller.GetPublic(order.Id, customerId: order.CustomerId, token: null);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    // H9 (compat): durante el período de gracia el enlace antiguo sigue funcionando.
    [Fact]
    public async Task GetPublic_LegacyEnabled_WithCustomerId_ReturnsOrder()
    {
        await using var db = TestDb.Create();
        var (order, tokens) = SeedOrder(db);
        var controller = CreateController(db, tokens, allowLegacy: true);

        var result = await controller.GetPublic(order.Id, customerId: order.CustomerId, token: null);

        Assert.IsType<OkObjectResult>(result.Result);
    }

    private static (Order Order, OrderTrackingTokenService Tokens) SeedOrder(CoreDbContext db)
    {
        var (tenantId, partner, customer, product) = TestDb.SeedCatalog(db, stock: 5);
        var order = new Order
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PartnerId = partner.Id,
            CustomerId = customer.Id,
            Status = "payment_pending",
            TotalAmount = 1000m,
            Currency = "CLP",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Items = [new OrderItem { Id = Guid.NewGuid(), ProductId = product.Id, Quantity = 1, UnitPrice = 1000m, TotalPrice = 1000m }]
        };
        db.Orders.Add(order);
        db.SaveChanges();
        return (order, TestDb.CreateTrackingTokenService());
    }

    private static OrdersController CreateController(
        CoreDbContext db,
        OrderTrackingTokenService tokens,
        bool allowLegacy)
    {
        var options = Options.Create(new OrderTrackingOptions
        {
            TrackingTokenSecret = "unit-test-tracking-secret-0123456789-abcdef",
            TrackingTokenTtlDays = 30,
            AllowLegacyPublicAccess = allowLegacy
        });

        var checkout = new OrderCheckoutService(
            db,
            new RecordingOrderNotificationService(),
            TestDb.CreateInventoryService(db),
            new GuestCustomerService(db),
            TestDb.CreateDeliveryPricingService());

        return new OrdersController(
            db,
            new TenantContext(),
            checkout,
            TestDb.CreateInventoryService(db),
            new RecordingOrderNotificationService(),
            tokens,
            options,
            TestDb.CreateSettlementService(db));
    }
}
