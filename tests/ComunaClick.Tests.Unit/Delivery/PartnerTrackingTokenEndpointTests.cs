using ComunaClick.Api.Modules.Customers;
using ComunaClick.Api.Modules.Delivery;
using ComunaClick.Api.Modules.Orders;
using ComunaClick.Api.Modules.Orders.Contracts;
using ComunaClick.Api.Persistence;
using ComunaClick.Tests.Unit.TestSupport;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace ComunaClick.Tests.Unit.Delivery;

public sealed class PartnerTrackingTokenEndpointTests
{
    private static CouriersController CreateController(CoreDbContext db, TenantContext tenantContext)
        => new(
            db,
            tenantContext,
            deliveryService: null!,
            payeeService: null!,
            mpOAuth: null!,
            trackingTokens: TestDb.CreateTrackingTokenService())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

    private static async Task<ComunaClick.Api.Persistence.Entities.Order> SeedPaidOrderAsync(
        CoreDbContext db,
        Guid tenantId,
        Guid partnerId,
        Guid customerId,
        Guid productId)
    {
        var checkout = new OrderCheckoutService(
            db,
            new RecordingOrderNotificationService(),
            TestDb.CreateInventoryService(db),
            new GuestCustomerService(db),
            TestDb.CreateDeliveryPricingService());

        var result = await checkout.CreateOrderAsync(
            tenantId,
            customerId,
            new OrderCreateRequest(
                partnerId,
                customerId,
                DeliveryFee: 0m,
                Currency: "CLP",
                Items: [new OrderItemCreateRequest(productId, 1, 1000m)],
                DeliveryAddress: "Calle Falsa 123, Santiago"));

        Assert.True(result.Success, result.ErrorMessage);
        return result.Order!;
    }

    // El dueño del pedido recibe un token que valida exactamente igual que el
    // del comprador (mismo HMAC que autoriza snapshot público y hub SignalR).
    [Fact]
    public async Task OwnerPartner_GetsValidTrackingToken()
    {
        await using var db = TestDb.Create();
        var (tenantId, partner, customer, product) = TestDb.SeedCatalog(db, stock: 5);
        var order = await SeedPaidOrderAsync(db, tenantId, partner.Id, customer.Id, product.Id);

        var tenantContext = new TenantContext();
        tenantContext.Set(tenantId, partner.Id);
        var controller = CreateController(db, tenantContext);

        var result = await controller.GetTrackingToken(order.Id);

        var ok = Assert.IsType<OkObjectResult>(result);
        var token = ok.Value!.GetType().GetProperty("token")!.GetValue(ok.Value) as string;
        Assert.False(string.IsNullOrWhiteSpace(token));

        // El token autoriza el seguimiento del pedido con el CustomerId correcto.
        Assert.True(TestDb.CreateTrackingTokenService().TryValidate(token, order.Id, out var customerId));
        Assert.Equal(customer.Id, customerId);
    }

    [Fact]
    public async Task UnknownOrder_ReturnsNotFound()
    {
        await using var db = TestDb.Create();
        var (tenantId, partner, _, _) = TestDb.SeedCatalog(db, stock: 1);
        var tenantContext = new TenantContext();
        tenantContext.Set(tenantId, partner.Id);
        var controller = CreateController(db, tenantContext);

        var result = await controller.GetTrackingToken(Guid.NewGuid());

        Assert.IsType<NotFoundResult>(result);
    }

    // Un comercio ajeno (otro partner del mismo tenant) no puede ver el tracking.
    [Fact]
    public async Task ForeignPartner_IsForbidden()
    {
        await using var db = TestDb.Create();
        var (tenantId, partner, customer, product) = TestDb.SeedCatalog(db, stock: 5);
        var order = await SeedPaidOrderAsync(db, tenantId, partner.Id, customer.Id, product.Id);

        var (otherTenantId, otherPartner, _, _) = TestDb.SeedCatalog(db, stock: 1);
        var tenantContext = new TenantContext();
        tenantContext.Set(otherTenantId, otherPartner.Id);
        var controller = CreateController(db, tenantContext);

        var result = await controller.GetTrackingToken(order.Id);

        Assert.IsType<ForbidResult>(result);
    }
}
