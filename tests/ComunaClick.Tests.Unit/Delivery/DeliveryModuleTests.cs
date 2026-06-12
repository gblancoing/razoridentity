using ComunaClick.Api.Configuration;
using ComunaClick.Api.Jobs;
using ComunaClick.Api.Modules.Delivery;
using ComunaClick.Api.Persistence;
using ComunaClick.Api.Persistence.Entities;
using ComunaClick.Tests.Unit.TestSupport;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace ComunaClick.Tests.Unit.Delivery;

public sealed class DeliveryModuleTests
{
    private static (Order Order, Courier Courier) SeedDeliveryOrder(CoreDbContext db, string status = DeliveryStatuses.Assigned)
    {
        var (tenantId, partner, customer, product) = TestDb.SeedCatalog(db, stock: 5);
        var courier = new Courier
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PartnerId = partner.Id,
            Name = "Pedro Repartos",
            Phone = "+56911112222",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        var order = new Order
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PartnerId = partner.Id,
            CustomerId = customer.Id,
            Status = "paid",
            TotalAmount = 1000m,
            Currency = "CLP",
            DeliveryType = DeliveryTypes.Delivery,
            DeliveryStatus = status,
            DeliveryAddress = "Calle Falsa 123",
            CourierId = courier.Id,
            CourierTokenKey = "clave-vigente",
            OriginLat = -33.45,
            OriginLng = -70.66,
            DestinationLat = -33.46,
            DestinationLng = -70.65,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.Couriers.Add(courier);
        db.Orders.Add(order);
        db.SaveChanges();
        return (order, courier);
    }

    // ----- Máquina de estados -----

    [Theory]
    [InlineData("pending", "assigned", true)]
    [InlineData("assigned", "in_transit", true)]
    [InlineData("picked_up", "delivered", true)]
    [InlineData("in_transit", "canceled", true)]
    [InlineData("in_transit", "assigned", false)]
    [InlineData("delivered", "in_transit", false)]
    [InlineData("canceled", "assigned", false)]
    public void DeliveryStatuses_CanTransition(string from, string to, bool expected)
    {
        Assert.Equal(expected, DeliveryStatuses.CanTransition(from, to, out _));
    }

    // ----- Token del repartidor -----

    [Fact]
    public void CourierToken_RoundTrip_Validates()
    {
        var service = TestDb.CreateCourierTokenService();
        var orderId = Guid.NewGuid();
        var token = service.Create(orderId, "clave-vigente");

        Assert.True(service.TryValidate(token, orderId, "clave-vigente"));
    }

    [Fact]
    public void CourierToken_WrongOrder_IsRejected()
    {
        var service = TestDb.CreateCourierTokenService();
        var token = service.Create(Guid.NewGuid(), "clave-vigente");

        Assert.False(service.TryValidate(token, Guid.NewGuid(), "clave-vigente"));
    }

    [Fact]
    public void CourierToken_RevokedKey_IsRejected()
    {
        // Reasignar/entregar/cancelar cambia o limpia la clave: el token previo muere.
        var service = TestDb.CreateCourierTokenService();
        var orderId = Guid.NewGuid();
        var token = service.Create(orderId, "clave-anterior");

        Assert.False(service.TryValidate(token, orderId, "clave-nueva"));
        Assert.False(service.TryValidate(token, orderId, null));
    }

    [Fact]
    public void CourierToken_BuyerTrackingToken_CannotReportGps()
    {
        // Un token de tracking del comprador no tiene el propósito "courier".
        var buyerTokens = TestDb.CreateTrackingTokenService();
        var courierTokens = TestDb.CreateCourierTokenService();
        var orderId = Guid.NewGuid();
        var buyerToken = buyerTokens.Create(orderId, Guid.NewGuid());

        Assert.False(courierTokens.TryValidate(buyerToken, orderId, "clave-vigente"));
    }

    // ----- Reporte de ubicación -----

    [Fact]
    public async Task UpdateCourierLocation_OutOfRange_IsRejected()
    {
        await using var db = TestDb.Create();
        var hub = new RecordingDeliveryHubContext();
        var service = TestDb.CreateDeliveryService(db, hub);
        var (order, _) = SeedDeliveryOrder(db);

        var result = await service.UpdateCourierLocationAsync(order.Id, 120, -70.6, null);

        Assert.False(result.Ok);
        Assert.Empty(hub.Sent);
    }

    [Fact]
    public async Task UpdateCourierLocation_NearbyPoint_BroadcastsWithoutPersisting()
    {
        await using var db = TestDb.Create();
        var hub = new RecordingDeliveryHubContext();
        var service = TestDb.CreateDeliveryService(db, hub);
        var (order, _) = SeedDeliveryOrder(db);

        var first = await service.UpdateCourierLocationAsync(order.Id, -33.4500, -70.6600, DateTimeOffset.UtcNow.AddSeconds(-10));
        // ~5 metros del punto anterior: difunde pero no persiste.
        var second = await service.UpdateCourierLocationAsync(order.Id, -33.45004, -70.66000, DateTimeOffset.UtcNow);

        Assert.True(first.Ok);
        Assert.True(second.Ok);
        Assert.Equal(2, hub.Sent.Count(x => x.Method == "CourierLocationChanged"));
        Assert.Equal(1, await db.DeliveryTrackings.CountAsync());
    }

    [Fact]
    public async Task UpdateCourierLocation_RegressiveTimestamp_IsDiscarded()
    {
        await using var db = TestDb.Create();
        var hub = new RecordingDeliveryHubContext();
        var service = TestDb.CreateDeliveryService(db, hub);
        var (order, _) = SeedDeliveryOrder(db);

        await service.UpdateCourierLocationAsync(order.Id, -33.45, -70.66, DateTimeOffset.UtcNow);
        hub.Sent.Clear();

        // Punto más antiguo que el último persistido: se descarta sin difundir.
        var stale = await service.UpdateCourierLocationAsync(order.Id, -33.46, -70.65, DateTimeOffset.UtcNow.AddMinutes(-5));

        Assert.True(stale.Ok);
        Assert.Empty(hub.Sent);
        Assert.Equal(1, await db.DeliveryTrackings.CountAsync());
    }

    [Fact]
    public async Task UpdateCourierLocation_FinishedDelivery_IsRejected()
    {
        await using var db = TestDb.Create();
        var hub = new RecordingDeliveryHubContext();
        var service = TestDb.CreateDeliveryService(db, hub);
        var (order, _) = SeedDeliveryOrder(db, DeliveryStatuses.Delivered);

        var result = await service.UpdateCourierLocationAsync(order.Id, -33.45, -70.66, null);

        Assert.False(result.Ok);
    }

    // ----- Cambios de estado -----

    [Fact]
    public async Task UpdateStatus_InvalidTransition_IsRejected()
    {
        await using var db = TestDb.Create();
        var hub = new RecordingDeliveryHubContext();
        var service = TestDb.CreateDeliveryService(db, hub);
        var (order, _) = SeedDeliveryOrder(db, DeliveryStatuses.Delivered);

        var result = await service.UpdateStatusAsync(order.Id, DeliveryStatuses.InTransit);

        Assert.False(result.Ok);
    }

    [Fact]
    public async Task UpdateStatus_AssignedToInTransit_EmitsBothBroadcasts()
    {
        // "Recogí el pedido" pasa por picked_up y termina en in_transit.
        await using var db = TestDb.Create();
        var hub = new RecordingDeliveryHubContext();
        var service = TestDb.CreateDeliveryService(db, hub);
        var (order, _) = SeedDeliveryOrder(db, DeliveryStatuses.Assigned);

        var result = await service.UpdateStatusAsync(order.Id, DeliveryStatuses.InTransit);

        Assert.True(result.Ok);
        Assert.Equal(2, hub.Sent.Count(x => x.Method == "DeliveryStatusChanged"));
        Assert.Equal(DeliveryStatuses.InTransit, (await db.Orders.SingleAsync(x => x.Id == order.Id)).DeliveryStatus);
    }

    [Fact]
    public async Task UpdateStatus_Delivered_RevokesCourierToken()
    {
        await using var db = TestDb.Create();
        var hub = new RecordingDeliveryHubContext();
        var service = TestDb.CreateDeliveryService(db, hub);
        var (order, _) = SeedDeliveryOrder(db, DeliveryStatuses.InTransit);

        var result = await service.UpdateStatusAsync(order.Id, DeliveryStatuses.Delivered);

        Assert.True(result.Ok);
        Assert.Null((await db.Orders.SingleAsync(x => x.Id == order.Id)).CourierTokenKey);
    }

    [Fact]
    public async Task UpdateStatus_Canceled_RevokesCourierToken()
    {
        await using var db = TestDb.Create();
        var hub = new RecordingDeliveryHubContext();
        var service = TestDb.CreateDeliveryService(db, hub);
        var (order, _) = SeedDeliveryOrder(db, DeliveryStatuses.Assigned);

        var result = await service.UpdateStatusAsync(order.Id, DeliveryStatuses.Canceled);

        Assert.True(result.Ok);
        Assert.Null((await db.Orders.SingleAsync(x => x.Id == order.Id)).CourierTokenKey);
    }

    // ----- Asignación y reasignación -----

    [Fact]
    public async Task AssignCourier_Reassign_RegeneratesTokenKey()
    {
        await using var db = TestDb.Create();
        var hub = new RecordingDeliveryHubContext();
        var service = TestDb.CreateDeliveryService(db, hub);
        var (order, courier) = SeedDeliveryOrder(db, DeliveryStatuses.Pending);

        var first = await service.AssignCourierAsync(order.Id, courier.Id);
        var keyAfterFirst = (await db.Orders.SingleAsync(x => x.Id == order.Id)).CourierTokenKey;
        var second = await service.AssignCourierAsync(order.Id, courier.Id);
        var keyAfterSecond = (await db.Orders.SingleAsync(x => x.Id == order.Id)).CourierTokenKey;

        Assert.NotNull(first.Result);
        Assert.NotNull(second.Result);
        Assert.NotNull(keyAfterFirst);
        Assert.NotNull(keyAfterSecond);
        // Reasignar regenera la clave: el link anterior queda revocado.
        Assert.NotEqual(keyAfterFirst, keyAfterSecond);

        var tokens = TestDb.CreateCourierTokenService();
        var firstToken = first.Result!.CourierLink.Split("token=")[1];
        Assert.False(tokens.TryValidate(Uri.UnescapeDataString(firstToken), order.Id, keyAfterSecond));
    }

    // ----- Hub: unirse al grupo exige token válido -----

    [Fact]
    public async Task JoinOrderTracking_InvalidToken_IsRejected()
    {
        await using var db = TestDb.Create();
        var hubContext = new RecordingDeliveryHubContext();
        var deliveryService = TestDb.CreateDeliveryService(db, hubContext);
        var (order, _) = SeedDeliveryOrder(db);

        var hub = new DeliveryHub(
            db,
            TestDb.CreateTrackingTokenService(),
            TestDb.CreateCourierTokenService(),
            deliveryService)
        {
            Groups = new RecordingDeliveryHubContext.NoopGroupManager()
        };

        await Assert.ThrowsAsync<HubException>(() => hub.JoinOrderTracking(order.Id, "token-falso"));

        // Token de otra orden tampoco sirve.
        var otherToken = TestDb.CreateCourierTokenService().Create(Guid.NewGuid(), "clave-vigente");
        await Assert.ThrowsAsync<HubException>(() => hub.JoinOrderTracking(order.Id, otherToken));
    }

    // ----- Tarifa (stub) -----

    [Fact]
    public async Task FlatRatePricing_UsesFlatFeeOrProviderBaseFee()
    {
        var provider = new DeliveryProvider { Id = Guid.NewGuid(), Name = "Moto Ya", BaseFee = 2500m, IsActive = true };

        var defaultService = new FlatRateDeliveryPricingService(Options.Create(new DeliveryOptions()));
        var flatService = new FlatRateDeliveryPricingService(Options.Create(new DeliveryOptions { FlatFee = 1990m }));

        Assert.Equal(0m, await defaultService.GetDeliveryFeeAsync(new DeliveryPricingContext(null, null, null, null, null)));
        Assert.Equal(2500m, await defaultService.GetDeliveryFeeAsync(new DeliveryPricingContext(provider, null, null, null, null)));
        Assert.Equal(1990m, await flatService.GetDeliveryFeeAsync(new DeliveryPricingContext(provider, null, null, null, null)));
    }

    // ----- Retención del historial -----

    [Fact]
    public async Task CleanupJob_KeepsOnlyLastPointForFinishedDeliveries()
    {
        await using var db = TestDb.Create();
        var hub = new RecordingDeliveryHubContext();
        var (order, courier) = SeedDeliveryOrder(db, DeliveryStatuses.Delivered);

        var baseTime = DateTimeOffset.UtcNow.AddDays(-10);
        for (var i = 0; i < 3; i++)
        {
            db.DeliveryTrackings.Add(new DeliveryTracking
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                CourierId = courier.Id,
                Lat = -33.45 + i * 0.001,
                Lng = -70.66,
                Status = DeliveryStatuses.InTransit,
                CreatedAt = baseTime.AddMinutes(i)
            });
        }

        var tracked = await db.Orders.SingleAsync(x => x.Id == order.Id);
        tracked.UpdatedAt = DateTimeOffset.UtcNow.AddDays(-8);
        await db.SaveChangesAsync();

        var job = new DeliveryTrackingCleanupJob(db, Options.Create(new DeliveryOptions()), NullLogger<DeliveryTrackingCleanupJob>.Instance);
        await job.RunAsync(CancellationToken.None);

        var remaining = await db.DeliveryTrackings.Where(x => x.OrderId == order.Id).ToListAsync();
        var last = Assert.Single(remaining);
        Assert.Equal(baseTime.AddMinutes(2), last.CreatedAt);
    }

    // ----- Distancia -----

    [Fact]
    public void DistanceMeters_IsPlausible()
    {
        // ~111 metros por cada 0.001° de latitud.
        var distance = DeliveryService.DistanceMeters(-33.45, -70.66, -33.451, -70.66);
        Assert.InRange(distance, 100, 125);
    }
}
