using ComunaClick.Api.Modules.Customers;
using ComunaClick.Api.Modules.Orders;
using ComunaClick.Api.Modules.Orders.Contracts;
using ComunaClick.Api.Persistence.Entities;
using ComunaClick.Tests.Unit.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ComunaClick.Tests.Unit.Orders;

public sealed class OrderCheckoutDeliveryFeeTests
{
    // H7: el cliente no puede forzar despacho $0 ni manipular el costo de envío.
    [Fact]
    public async Task CreateOrder_IgnoresClientDeliveryFee_WhenNoProvider()
    {
        await using var db = TestDb.Create();
        var (tenantId, partner, customer, product) = TestDb.SeedCatalog(db, stock: 5, price: 1000m);

        var service = CreateService(db);

        var result = await service.CreateOrderAsync(
            tenantId,
            customer.Id,
            new OrderCreateRequest(
                partner.Id,
                customer.Id,
                DeliveryFee: 99999m, // intento de manipulación
                Currency: "CLP",
                Items: [new OrderItemCreateRequest(product.Id, 2, product.Price)]));

        Assert.True(result.Success);
        Assert.Equal(0m, result.Order!.DeliveryFee);
        Assert.Equal(2000m, result.Order.TotalAmount);
    }

    // H7: con proveedor de despacho, el cobro usa su tarifa base, no el valor del request.
    [Fact]
    public async Task CreateOrder_UsesProviderBaseFee_NotClientValue()
    {
        await using var db = TestDb.Create();
        var (tenantId, partner, customer, product) = TestDb.SeedCatalog(db, stock: 5, price: 1000m);

        var provider = new DeliveryProvider
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Despacho local",
            BaseFee = 1500m,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.DeliveryProviders.Add(provider);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var result = await service.CreateOrderAsync(
            tenantId,
            customer.Id,
            new OrderCreateRequest(
                partner.Id,
                customer.Id,
                DeliveryFee: 0m, // el cliente intenta no pagar despacho
                Currency: "CLP",
                Items: [new OrderItemCreateRequest(product.Id, 1, product.Price)],
                DeliveryProviderId: provider.Id));

        Assert.True(result.Success);
        Assert.Equal(1500m, result.Order!.DeliveryFee);
        Assert.Equal(2500m, result.Order.TotalAmount);
    }

    private static OrderCheckoutService CreateService(ComunaClick.Api.Persistence.CoreDbContext db)
        => new(
            db,
            new RecordingOrderNotificationService(),
            TestDb.CreateInventoryService(db),
            new GuestCustomerService(db),
            TestDb.CreateDeliveryPricingService());
}
