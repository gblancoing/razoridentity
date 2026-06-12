using ComunaClick.Api.Configuration;
using ComunaClick.Api.Modules.Delivery;
using ComunaClick.Api.Modules.Marketplace;
using ComunaClick.Api.Persistence.Entities;
using ComunaClick.Tests.Unit.TestSupport;
using Microsoft.Extensions.Options;
using Xunit;

namespace ComunaClick.Tests.Unit.Delivery;

public sealed class DeliveryPricingAndSettlementTests
{
    // Coordenadas reales: local en Las Cabras y destinos a distintas distancias.
    private const double OriginLat = -34.183862;
    private const double OriginLng = -71.351963;

    private static DeliveryFeeCalculator CreateCalculator(DeliveryPricingOptions? options = null)
        => new(Options.Create(options ?? new DeliveryPricingOptions()));

    private static readonly TimeZoneInfo Chile = ResolveChileTz();

    private static TimeZoneInfo ResolveChileTz()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/Santiago");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Pacific SA Standard Time");
        }
    }

    /// <summary>Instante UTC cuya hora local en Chile es exactamente la pedida.</summary>
    private static DateTimeOffset AtChileHour(int hour)
    {
        var local = new DateTime(2026, 6, 15, hour, 30, 0, DateTimeKind.Unspecified);
        return new DateTimeOffset(local, Chile.GetUtcOffset(local)).ToUniversalTime();
    }

    /// <summary>Punto a ~X km al norte del origen (1° de latitud ≈ 111.32 km).</summary>
    private static (double Lat, double Lng) KmNorth(double km) => (OriginLat + (km / 111.32), OriginLng);

    [Fact]
    public void DayProfile_WithinBaseRadius_ChargesBaseFee()
    {
        var (lat, lng) = KmNorth(1.5);
        var result = CreateCalculator().Calculate(OriginLat, OriginLng, lat, lng, AtChileHour(12));

        Assert.NotNull(result);
        Assert.Equal("diurno", result!.AppliedProfile);
        Assert.Equal(0, result.ExcessKm);
        Assert.Equal(2800m, result.TotalFee);
    }

    [Fact]
    public void DayProfile_ExactlyAtRadiusLimit_ChargesBaseFeeOnly()
    {
        var (lat, lng) = KmNorth(2.0);
        var result = CreateCalculator().Calculate(OriginLat, OriginLng, lat, lng, AtChileHour(12));

        Assert.NotNull(result);
        Assert.Equal(0, result!.ExcessKm);
        Assert.Equal(2800m, result.TotalFee);
    }

    [Fact]
    public void DayProfile_BeyondRadius_ChargesPerStartedKm()
    {
        // ~5.3 km → excedente 3.3 km → ceil = 4 km (la fracción cuenta como km entero).
        var (lat, lng) = KmNorth(5.3);
        var result = CreateCalculator().Calculate(OriginLat, OriginLng, lat, lng, AtChileHour(12));

        Assert.NotNull(result);
        Assert.Equal("diurno", result!.AppliedProfile);
        Assert.Equal(4, result.ExcessKm);
        Assert.Equal(2800m + (4 * 1250m), result.TotalFee);
    }

    [Fact]
    public void NightProfile_AfterMidnightChileanTime_AppliesNightRates()
    {
        // 00:30 hora chilena (cruce de medianoche): perfil nocturno.
        var (lat, lng) = KmNorth(5.3);
        var result = CreateCalculator().Calculate(OriginLat, OriginLng, lat, lng, AtChileHour(0));

        Assert.NotNull(result);
        Assert.Equal("nocturno", result!.AppliedProfile);
        Assert.Equal(3500m + (4 * 1600m), result.TotalFee);
    }

    [Fact]
    public void NightProfile_JustBeforeMidnight_StillDay()
    {
        var (lat, lng) = KmNorth(1.0);
        var result = CreateCalculator().Calculate(OriginLat, OriginLng, lat, lng, AtChileHour(23));

        Assert.NotNull(result);
        Assert.Equal("diurno", result!.AppliedProfile);
        Assert.Equal(2800m, result.TotalFee);
    }

    [Fact]
    public void MaxFeeCap_LimitsAbsurdFares()
    {
        var options = new DeliveryPricingOptions { MaxFee = 5000m, MaxDistanceKm = 0 };
        var (lat, lng) = KmNorth(12);
        var result = CreateCalculator(options).Calculate(OriginLat, OriginLng, lat, lng, AtChileHour(12));

        Assert.NotNull(result);
        Assert.Equal(5000m, result!.TotalFee);
    }

    [Fact]
    public void BeyondCoverageRadius_DeliveryNotAvailable()
    {
        var options = new DeliveryPricingOptions { MaxDistanceKm = 10 };
        var (lat, lng) = KmNorth(12);

        var ex = Assert.Throws<DeliveryOutOfRangeException>(
            () => CreateCalculator(options).Calculate(OriginLat, OriginLng, lat, lng, AtChileHour(12)));
        Assert.True(ex.DistanceKm > ex.MaxDistanceKm);
    }

    [Fact]
    public void MissingOrInvalidCoordinates_ReturnsNullForFallback()
    {
        var calculator = CreateCalculator();
        Assert.Null(calculator.Calculate(null, null, -34.2, -71.3, AtChileHour(12)));
        Assert.Null(calculator.Calculate(OriginLat, OriginLng, null, null, AtChileHour(12)));
        Assert.Null(calculator.Calculate(OriginLat, OriginLng, 200, 300, AtChileHour(12)));
    }

    [Fact]
    public async Task DynamicPricing_WithoutCoordinates_FallsBackToProviderBaseFee()
    {
        var service = new DynamicDeliveryPricingService(
            CreateCalculator(),
            Options.Create(new ComunaClick.Api.Configuration.DeliveryOptions()));

        var provider = new DeliveryProvider { Id = Guid.NewGuid(), Name = "Zona", BaseFee = 1900m, IsActive = true };
        var fee = await service.GetDeliveryFeeAsync(new DeliveryPricingContext(provider, null, null, null, null));

        Assert.Equal(1900m, fee);
    }

    [Fact]
    public async Task DynamicPricing_WithCoordinates_UsesDistanceFare()
    {
        var service = new DynamicDeliveryPricingService(
            CreateCalculator(),
            Options.Create(new ComunaClick.Api.Configuration.DeliveryOptions()));

        var provider = new DeliveryProvider { Id = Guid.NewGuid(), Name = "Zona", BaseFee = 1900m, IsActive = true };
        var (lat, lng) = KmNorth(1.0);
        var fee = await service.GetDeliveryFeeAsync(new DeliveryPricingContext(provider, OriginLat, OriginLng, lat, lng));

        // Según la hora a la que corra el test aplica diurno (2800) o nocturno (3500).
        Assert.True(fee is 2800m or 3500m, $"fee inesperado: {fee}");
    }

    // ----- Split / liquidación del transportista -----

    private static (Order Order, Courier Courier) SeedDeliveryOrder(
        ComunaClick.Api.Persistence.CoreDbContext db,
        decimal deliveryFee,
        bool assignCourier = true)
    {
        var tenantId = Guid.NewGuid();
        var partnerId = Guid.NewGuid();
        var courier = new Courier
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PartnerId = partnerId,
            Name = "Repartidor Test",
            Phone = "+56900000000",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        var order = new Order
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PartnerId = partnerId,
            CustomerId = Guid.NewGuid(),
            Status = "paid",
            Subtotal = 10000m,
            DeliveryFee = deliveryFee,
            TotalAmount = 10000m + deliveryFee,
            GrossAmount = 10000m + deliveryFee,
            NetAmount = 10000m + deliveryFee,
            Currency = "CLP",
            DeliveryType = DeliveryTypes.Delivery,
            DeliveryStatus = DeliveryStatuses.Pending,
            CourierId = assignCourier ? courier.Id : null,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.Couriers.Add(courier);
        db.Orders.Add(order);
        db.SaveChanges();
        return (order, courier);
    }

    [Fact]
    public async Task Settlement_Split_AlwaysAddsUpExactly()
    {
        await using var db = TestDb.Create();
        var (order, _) = SeedDeliveryOrder(db, deliveryFee: 3000m);
        var service = TestDb.CreateSettlementService(db);

        var payment = new Payment { Id = Guid.NewGuid(), TenantId = order.TenantId, OrderId = order.Id, Amount = order.GrossAmount };
        var settlement = await service.CreateForPaidOrderAsync(order, payment);
        Assert.NotNull(settlement);

        // Fee MP real conciliado desde el webhook: $419 sobre un pago de $13.000
        // (números impares a propósito para verificar el redondeo).
        await service.ReconcileMercadoPagoFeeAsync(order.Id, totalMpFee: 419m, paymentGross: order.GrossAmount);

        var stored = db.DeliverySettlements.Single(x => x.OrderId == order.Id);
        Assert.Equal(3000m, stored.GrossAmount);
        Assert.NotNull(stored.MercadoPagoFeeAmount);
        // bruto = feeMP + feeCC + neto, exacto: el residuo del redondeo lo absorbe el neto.
        Assert.Equal(stored.GrossAmount, stored.MercadoPagoFeeAmount!.Value + stored.PlatformFeeAmount + stored.NetToCourierAmount);
        // Comisión CC por defecto 10% → $300; prorrateo MP: 419 * 3000/13000 ≈ $97.
        Assert.Equal(300m, stored.PlatformFeeAmount);
        Assert.Equal(97m, stored.MercadoPagoFeeAmount);
        Assert.Equal(2603m, stored.NetToCourierAmount);
    }

    [Fact]
    public async Task Settlement_ManualPayment_HasZeroMpFee()
    {
        await using var db = TestDb.Create();
        var (order, _) = SeedDeliveryOrder(db, deliveryFee: 3000m);
        var service = TestDb.CreateSettlementService(db);

        var settlement = await service.CreateForPaidOrderAsync(order, payment: null);

        Assert.NotNull(settlement);
        Assert.Equal(0m, settlement!.MercadoPagoFeeAmount);
        Assert.Equal(3000m - 300m, settlement.NetToCourierAmount);
        Assert.Equal(settlement.GrossAmount, settlement.MercadoPagoFeeAmount!.Value + settlement.PlatformFeeAmount + settlement.NetToCourierAmount);
    }

    [Fact]
    public async Task Settlement_CourierWithoutMpAccount_IsMarkedManual()
    {
        await using var db = TestDb.Create();
        var (order, _) = SeedDeliveryOrder(db, deliveryFee: 3000m);
        var service = TestDb.CreateSettlementService(db);

        var settlement = await service.CreateForPaidOrderAsync(order, payment: null);

        // Repartidor sin Seller/cuenta MP conectada → liquidación manual marcada como tal.
        Assert.Equal("manual", settlement!.Status);
    }

    [Fact]
    public async Task Settlement_CourierWithConnectedAccount_IsPending()
    {
        await using var db = TestDb.Create();
        var (order, courier) = SeedDeliveryOrder(db, deliveryFee: 3000m);

        // Cuenta MP del transportista conectada (fila Seller con Id = courier.Id).
        db.Sellers.Add(new Seller { Id = courier.Id, TenantId = courier.TenantId, Name = courier.Name, Email = "c@test.cl" });
        db.SellerMercadoPagoAccounts.Add(new SellerMercadoPagoAccount
        {
            Id = Guid.NewGuid(),
            SellerId = courier.Id,
            ConnectionStatus = "connected",
            AccessTokenEncrypted = "protected-token",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var settlement = await TestDb.CreateSettlementService(db).CreateForPaidOrderAsync(order, payment: null);

        Assert.Equal("pending", settlement!.Status);
    }

    [Fact]
    public async Task Settlement_IsIdempotentPerOrder()
    {
        await using var db = TestDb.Create();
        var (order, _) = SeedDeliveryOrder(db, deliveryFee: 3000m);
        var service = TestDb.CreateSettlementService(db);

        var first = await service.CreateForPaidOrderAsync(order, payment: null);
        var second = await service.CreateForPaidOrderAsync(order, payment: null);

        Assert.Equal(first!.Id, second!.Id);
        Assert.Single(db.DeliverySettlements);
    }

    [Fact]
    public async Task Settlement_NotCreatedForPickupOrZeroFeeOrders()
    {
        await using var db = TestDb.Create();
        var (order, _) = SeedDeliveryOrder(db, deliveryFee: 0m);
        var service = TestDb.CreateSettlementService(db);

        Assert.Null(await service.CreateForPaidOrderAsync(order, payment: null));

        order.DeliveryFee = 2000m;
        order.DeliveryType = DeliveryTypes.Pickup;
        Assert.Null(await service.CreateForPaidOrderAsync(order, payment: null));
    }

    [Fact]
    public async Task Settlement_MarkSettled_OnlyOnce()
    {
        await using var db = TestDb.Create();
        var (order, _) = SeedDeliveryOrder(db, deliveryFee: 3000m);
        var service = TestDb.CreateSettlementService(db);
        var settlement = await service.CreateForPaidOrderAsync(order, payment: null);

        var (ok, _) = await service.MarkSettledAsync(settlement!.Id, "transferencia 12345");
        Assert.True(ok);

        var (okAgain, error) = await service.MarkSettledAsync(settlement.Id, null);
        Assert.False(okAgain);
        Assert.NotNull(error);
    }
}
