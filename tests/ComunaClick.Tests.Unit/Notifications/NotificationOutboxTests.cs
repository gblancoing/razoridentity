using ComunaClick.Api.Integrations.Notifications;
using ComunaClick.Api.Modules.Customers;
using ComunaClick.Api.Modules.Orders;
using ComunaClick.Api.Modules.Orders.Contracts;
using ComunaClick.Api.Persistence.Entities;
using ComunaClick.Tests.Unit.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ComunaClick.Tests.Unit.Notifications;

public sealed class NotificationOutboxTests
{
    // H4: una notificación que falla queda pendiente con reintentos y termina enviándose.
    [Fact]
    public async Task Processor_RetriesUntilSenderSucceeds_AndMarksSent()
    {
        await using var db = TestDb.Create();
        var outbox = SeedPendingEmail(db, maxAttempts: 5);
        var sender = new FakeEmailSender(failuresBeforeSuccess: 2);
        var processor = TestDb.CreateOutboxProcessor(db, sender);

        await processor.ProcessPendingAsync();
        var afterFirst = await Reload(db, outbox.Id);
        Assert.Equal("pending", afterFirst.Status);
        Assert.Equal(1, afterFirst.Attempts);
        Assert.NotNull(afterFirst.LastError);

        await processor.ProcessPendingAsync();
        Assert.Equal("pending", (await Reload(db, outbox.Id)).Status);

        await processor.ProcessPendingAsync();
        var afterThird = await Reload(db, outbox.Id);
        Assert.Equal("sent", afterThird.Status);
        Assert.NotNull(afterThird.SentAt);
        Assert.Equal(1, sender.SuccessfulSends);
        Assert.Equal(3, sender.SendAttempts);
    }

    // H4: tras agotar reintentos la notificación queda en "failed" (no se pierde).
    [Fact]
    public async Task Processor_ExhaustsAttempts_MarksFailedButKeepsRow()
    {
        await using var db = TestDb.Create();
        var outbox = SeedPendingEmail(db, maxAttempts: 2);
        var sender = new FakeEmailSender { AlwaysFail = true };
        var processor = TestDb.CreateOutboxProcessor(db, sender);

        await processor.ProcessPendingAsync();
        Assert.Equal("pending", (await Reload(db, outbox.Id)).Status);

        await processor.ProcessPendingAsync();
        var failed = await Reload(db, outbox.Id);
        Assert.Equal("failed", failed.Status);
        Assert.Equal(2, failed.Attempts);
        Assert.NotNull(failed.LastError);

        // Estado terminal: el worker no lo vuelve a tomar.
        await processor.ProcessPendingAsync();
        Assert.Equal(2, sender.SendAttempts);
        Assert.Equal(1, await db.NotificationOutbox.IgnoreQueryFilters().CountAsync());
    }

    // H4: crear la orden no envía SMTP; solo encola la confirmación del comprador.
    [Fact]
    public async Task CreateOrder_DoesNotSendEmailInline_EnqueuesBuyerNotification()
    {
        await using var db = TestDb.Create();
        var (tenantId, partner, customer, product) = TestDb.SeedCatalog(db, stock: 3);
        var sender = new FakeEmailSender();
        var notifications = TestDb.CreateOrderNotificationService(db);

        var service = new OrderCheckoutService(
            db,
            notifications,
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
        Assert.Equal(0, sender.SendAttempts); // nada de SMTP dentro del request

        var rows = await db.NotificationOutbox.IgnoreQueryFilters().ToListAsync();
        Assert.Single(rows);
        Assert.Equal(NotificationKinds.BuyerOrder, rows[0].Kind);
        Assert.Equal("pending", rows[0].Status);
    }

    // H5: la orden recién creada (payment_pending) NO notifica al comercio; el aviso ocurre al pagar.
    [Fact]
    public async Task PartnerIsNotNotifiedOnCreation_OnlyWhenPaid_AndIdempotent()
    {
        await using var db = TestDb.Create();
        var (tenantId, partner, customer, product) = TestDb.SeedCatalog(db, stock: 3);
        var notifications = TestDb.CreateOrderNotificationService(db);

        var service = new OrderCheckoutService(
            db,
            notifications,
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

        var orderId = result.Order!.Id;

        Assert.Equal(0, await db.NotificationOutbox.IgnoreQueryFilters()
            .CountAsync(x => x.Kind == NotificationKinds.PartnerOrderPaid));

        // Pago confirmado por dos vías distintas → un solo aviso al comercio.
        await notifications.NotifyPartnerOrderPaidAsync(orderId);
        await notifications.NotifyPartnerOrderPaidAsync(orderId);

        Assert.Equal(1, await db.NotificationOutbox.IgnoreQueryFilters()
            .CountAsync(x => x.Kind == NotificationKinds.PartnerOrderPaid));
    }

    // H6: recordatorios de pago con email nulo no lanzan excepción ni encolan nada.
    [Fact]
    public async Task PaymentPendingReminders_WithNullEmail_DoNotThrow()
    {
        await using var db = TestDb.Create();
        var notifications = TestDb.CreateOrderNotificationService(db);

        var partner = new Partner { Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), Type = "A", Name = "Comercio" };
        var customer = new Customer { Id = Guid.NewGuid(), TenantId = partner.TenantId, Email = null };
        var order = new Order { Id = Guid.NewGuid(), TenantId = partner.TenantId, PartnerId = partner.Id, CustomerId = customer.Id, Status = "payment_pending", TotalAmount = 1000m, Currency = "CLP" };
        var booking = new Booking { Id = Guid.NewGuid(), TenantId = partner.TenantId, PartnerId = partner.Id, CustomerId = customer.Id, Status = "payment_pending", Amount = 1000m, Currency = "CLP" };

        await notifications.NotifyBuyerPaymentPendingAsync(order, partner, customer);
        await notifications.NotifyBuyerBookingPaymentPendingAsync(booking, partner, customer, "Corte de pelo");

        Assert.Equal(0, await db.NotificationOutbox.IgnoreQueryFilters().CountAsync());
    }

    private static NotificationOutbox SeedPendingEmail(
        ComunaClick.Api.Persistence.CoreDbContext db,
        int maxAttempts)
    {
        var now = DateTimeOffset.UtcNow;
        var row = new NotificationOutbox
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Channel = "email",
            Kind = NotificationKinds.PartnerOrderPaid,
            ReferenceId = Guid.NewGuid(),
            Recipient = "partner@test.cl",
            Subject = "Nueva venta",
            Body = "Detalle de la venta",
            Status = "pending",
            Attempts = 0,
            MaxAttempts = maxAttempts,
            NextAttemptAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.NotificationOutbox.Add(row);
        db.SaveChanges();
        return row;
    }

    private static async Task<NotificationOutbox> Reload(
        ComunaClick.Api.Persistence.CoreDbContext db,
        Guid id)
        => await db.NotificationOutbox.IgnoreQueryFilters().AsNoTracking().SingleAsync(x => x.Id == id);
}
