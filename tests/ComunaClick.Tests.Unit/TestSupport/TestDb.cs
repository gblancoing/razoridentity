using ComunaClick.Api.Configuration;
using ComunaClick.Api.Integrations.Notifications;
using ComunaClick.Api.Modules.Catalog;
using ComunaClick.Api.Modules.Orders;
using ComunaClick.Api.Persistence;
using ComunaClick.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ComunaClick.Tests.Unit.TestSupport;

internal static class TestDb
{
    public static CoreDbContext Create(string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase(databaseName ?? $"tests-{Guid.NewGuid():N}")
            .Options;
        return new CoreDbContext(options, new TenantContext());
    }

    public static ProductInventoryService CreateInventoryService(CoreDbContext db)
        => new(
            db,
            new StubStockNotificationService(),
            Options.Create(new InventoryOptions()),
            NullLogger<ProductInventoryService>.Instance);

    public static ComunaClick.Api.Modules.Delivery.FlatRateDeliveryPricingService CreateDeliveryPricingService()
        => new(Options.Create(new DeliveryOptions()));

    public static ComunaClick.Api.Modules.Delivery.DeliveryCourierTokenService CreateCourierTokenService(
        DeliveryOptions? deliveryOptions = null,
        TimeProvider? timeProvider = null)
        => new(
            Options.Create(new OrderTrackingOptions
            {
                TrackingTokenSecret = "unit-test-tracking-secret-0123456789-abcdef",
                TrackingTokenTtlDays = 30
            }),
            Options.Create(deliveryOptions ?? new DeliveryOptions()),
            timeProvider ?? TimeProvider.System);

    public static ComunaClick.Api.Modules.Delivery.DeliverySettlementService CreateSettlementService(
        CoreDbContext db,
        ComunaClick.Api.Configuration.DeliveryPricingOptions? pricingOptions = null)
        => new(
            db,
            new ComunaClick.Api.Modules.Marketplace.FeeCalculator(),
            new ComunaClick.Api.Modules.Delivery.CourierPayeeService(db),
            Options.Create(pricingOptions ?? new ComunaClick.Api.Configuration.DeliveryPricingOptions()));

    public static ComunaClick.Api.Modules.Delivery.DeliveryService CreateDeliveryService(
        CoreDbContext db,
        RecordingDeliveryHubContext hub,
        DeliveryOptions? deliveryOptions = null)
        => new(
            db,
            hub,
            CreateCourierTokenService(deliveryOptions),
            CreateSettlementService(db),
            Options.Create(deliveryOptions ?? new DeliveryOptions()));

    public static OrderNotificationOptions NotificationOptions(bool enabled = true)
        => new()
        {
            Enabled = enabled,
            EnableEmail = true,
            EnableBuyerEmail = true,
            EnableWhatsAppWebhook = false,
            OutboxRetryBaseSeconds = 0,
            OutboxMaxAttempts = 5,
            OutboxBatchSize = 25
        };

    public static OrderTrackingTokenService CreateTrackingTokenService(OrderTrackingOptions? options = null)
        => new(
            Options.Create(options ?? new OrderTrackingOptions
            {
                TrackingTokenSecret = "unit-test-tracking-secret-0123456789-abcdef",
                TrackingTokenTtlDays = 30
            }),
            TimeProvider.System);

    public static OrderNotificationService CreateOrderNotificationService(
        CoreDbContext db,
        OrderNotificationOptions? options = null)
        => new(
            Options.Create(options ?? NotificationOptions()),
            db,
            CreateTrackingTokenService(),
            NullLogger<OrderNotificationService>.Instance);

    public static NotificationOutboxProcessor CreateOutboxProcessor(
        CoreDbContext db,
        IEmailSender emailSender,
        IWhatsAppSender? whatsAppSender = null,
        OrderNotificationOptions? options = null)
        => new(
            db,
            emailSender,
            whatsAppSender ?? new FakeWhatsAppSender(),
            Options.Create(options ?? NotificationOptions()),
            TimeProvider.System,
            NullLogger<NotificationOutboxProcessor>.Instance);

    public static (Guid TenantId, Partner Partner, Customer Customer, Product Product) SeedCatalog(
        CoreDbContext db,
        int stock,
        decimal price = 1000m)
    {
        var tenantId = Guid.NewGuid();
        var partner = new Partner
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Type = "C",
            Name = $"Partner {Guid.NewGuid():N}",
            Email = "partner@test.cl",
            Phone = "+56911111111",
            IsVisible = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = "buyer@test.cl",
            FullName = "Compradora Test",
            Phone = "+56922222222",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        var product = new Product
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PartnerId = partner.Id,
            Name = $"Producto {Guid.NewGuid():N}",
            Price = price,
            Currency = "CLP",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.Partners.Add(partner);
        db.Customers.Add(customer);
        db.Products.Add(product);
        db.ProductInventories.Add(new ProductInventory
        {
            ProductId = product.Id,
            Quantity = stock,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        db.SaveChanges();

        return (tenantId, partner, customer, product);
    }
}
