using ComunaClick.Api.Modules.Payments;
using ComunaClick.Api.Modules.Payments.Contracts;
using ComunaClick.Api.Persistence;
using ComunaClick.Api.Persistence.Entities;
using ComunaClick.Tests.Unit.TestSupport;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ComunaClick.Tests.Unit.Payments;

public sealed class PaymentsProviderNotifyTests
{
    private const string ValidKey = "unit-test-internal-webhook-key-0123456789";

    [Fact]
    public async Task ProviderNotify_KeyNotConfigured_Returns503AndDoesNotApprove()
    {
        await using var db = TestDb.Create();
        var payment = SeedPayment(db);
        var controller = CreateController(db, configuredKey: null, headerKey: ValidKey);

        var result = await controller.ProviderNotify(NotifyRequest(payment.Id, "approved"));

        var status = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, status.StatusCode);
        Assert.Equal("pending", (await Reload(db, payment.Id)).Status);
    }

    [Fact]
    public async Task ProviderNotify_PlaceholderKey_Returns503AndDoesNotApprove()
    {
        await using var db = TestDb.Create();
        var payment = SeedPayment(db);
        var controller = CreateController(db, configuredKey: "CHANGE_ME", headerKey: "CHANGE_ME");

        var result = await controller.ProviderNotify(NotifyRequest(payment.Id, "approved"));

        var status = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, status.StatusCode);
        Assert.Equal("pending", (await Reload(db, payment.Id)).Status);
    }

    [Fact]
    public async Task ProviderNotify_MissingHeader_ReturnsUnauthorizedAndDoesNotApprove()
    {
        await using var db = TestDb.Create();
        var payment = SeedPayment(db);
        var controller = CreateController(db, configuredKey: ValidKey, headerKey: null);

        var result = await controller.ProviderNotify(NotifyRequest(payment.Id, "approved"));

        Assert.IsType<UnauthorizedResult>(result);
        Assert.Equal("pending", (await Reload(db, payment.Id)).Status);
    }

    [Fact]
    public async Task ProviderNotify_WrongKey_ReturnsUnauthorizedAndDoesNotApprove()
    {
        await using var db = TestDb.Create();
        var payment = SeedPayment(db);
        var controller = CreateController(db, configuredKey: ValidKey, headerKey: "wrong-key-wrong-key-wrong-key-123");

        var result = await controller.ProviderNotify(NotifyRequest(payment.Id, "approved"));

        Assert.IsType<UnauthorizedResult>(result);
        Assert.Equal("pending", (await Reload(db, payment.Id)).Status);
    }

    [Fact]
    public async Task ProviderNotify_StatusOutsideWhitelist_ReturnsBadRequestAndDoesNotApprove()
    {
        await using var db = TestDb.Create();
        var payment = SeedPayment(db);
        var controller = CreateController(db, configuredKey: ValidKey, headerKey: ValidKey);

        var result = await controller.ProviderNotify(NotifyRequest(payment.Id, "super_paid_trust_me"));

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("pending", (await Reload(db, payment.Id)).Status);
    }

    [Fact]
    public async Task ProviderNotify_AmountMismatch_ReturnsBadRequestAndDoesNotApprove()
    {
        await using var db = TestDb.Create();
        var payment = SeedPayment(db, amount: 10000m);
        var controller = CreateController(db, configuredKey: ValidKey, headerKey: ValidKey);

        var result = await controller.ProviderNotify(NotifyRequest(payment.Id, "approved", amount: 1m));

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("pending", (await Reload(db, payment.Id)).Status);
    }

    [Fact]
    public async Task ProviderNotify_ValidApprovedNotification_MarksOrderPaidAndFulfillsOnce()
    {
        await using var db = TestDb.Create();
        var (tenantId, partner, customer, product) = TestDb.SeedCatalog(db, stock: 5);
        var order = new Order
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PartnerId = partner.Id,
            CustomerId = customer.Id,
            Status = "payment_pending",
            TotalAmount = 2000m,
            Currency = "CLP",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Items =
            [
                new OrderItem
                {
                    Id = Guid.NewGuid(),
                    ProductId = product.Id,
                    Quantity = 2,
                    UnitPrice = 1000m,
                    TotalPrice = 2000m
                }
            ]
        };
        db.Orders.Add(order);
        var payment = SeedPayment(db, amount: 2000m, orderId: order.Id, tenantId: tenantId);

        var controller = CreateController(db, configuredKey: ValidKey, headerKey: ValidKey);

        var first = await controller.ProviderNotify(NotifyRequest(payment.Id, "approved", amount: 2000m, eventId: "evt-1"));
        var second = await controller.ProviderNotify(NotifyRequest(payment.Id, "approved", amount: 2000m, eventId: "evt-2"));

        Assert.IsType<OkResult>(first);
        Assert.IsType<OkResult>(second);
        Assert.Equal("approved", (await Reload(db, payment.Id)).Status);

        var savedOrder = await db.Orders.AsNoTracking().SingleAsync(x => x.Id == order.Id);
        Assert.Equal("paid", savedOrder.Status);
        Assert.NotNull(savedOrder.InventoryFulfilledAt);

        var inventory = await db.ProductInventories.AsNoTracking().SingleAsync(x => x.ProductId == product.Id);
        Assert.Equal(3, inventory.Quantity);
    }

    private static PaymentsController CreateController(CoreDbContext db, string? configuredKey, string? headerKey)
    {
        var settings = new Dictionary<string, string?>();
        if (configuredKey is not null)
        {
            settings["Payments:InternalWebhookKey"] = configuredKey;
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var controller = new PaymentsController(
            db,
            configuration,
            new TenantContext(),
            TestDb.CreateInventoryService(db),
            new RecordingOrderNotificationService(),
            NullLogger<PaymentsController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        if (headerKey is not null)
        {
            controller.HttpContext.Request.Headers["X-Internal-Key"] = headerKey;
        }

        return controller;
    }

    private static Payment SeedPayment(
        CoreDbContext db,
        decimal amount = 10000m,
        Guid? orderId = null,
        Guid? tenantId = null)
    {
        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId ?? Guid.NewGuid(),
            OrderId = orderId,
            Provider = "transbank",
            ExternalReference = $"ref-{Guid.NewGuid():N}",
            Amount = amount,
            Currency = "CLP",
            Status = "pending",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Payments.Add(payment);
        db.SaveChanges();
        return payment;
    }

    private static PaymentProviderNotifyRequest NotifyRequest(
        Guid paymentId,
        string status,
        decimal? amount = null,
        string eventId = "evt-default")
        => new(paymentId, null, eventId, status, null, amount);

    private static async Task<Payment> Reload(CoreDbContext db, Guid paymentId)
        => await db.Payments.AsNoTracking().IgnoreQueryFilters().SingleAsync(x => x.Id == paymentId);
}
