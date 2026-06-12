using ComunaClick.Api.Configuration;
using ComunaClick.Api.Modules.Customers;
using ComunaClick.Api.Modules.Delivery;
using ComunaClick.Api.Modules.Orders;
using ComunaClick.Api.Modules.Orders.Contracts;
using ComunaClick.Api.Persistence;
using ComunaClick.Api.Persistence.Entities;
using ComunaClick.Tests.Unit.TestSupport;
using Microsoft.Extensions.Options;
using Xunit;

namespace ComunaClick.Tests.Unit.Checkout;

public sealed class DeliveryQuoteServiceTests
{
    // Coordenadas reales: local en Las Cabras y destinos a distintas distancias.
    private const double OriginLat = -34.183862;
    private const double OriginLng = -71.351963;

    /// <summary>Punto a ~X km al norte del origen (1° de latitud ≈ 111.32 km).</summary>
    private static (double Lat, double Lng) KmNorth(double km) => (OriginLat + (km / 111.32), OriginLng);

    private static DeliveryQuoteService CreateQuoteService(CoreDbContext db, DeliveryPricingOptions? pricing = null)
    {
        var calculator = new DeliveryFeeCalculator(Options.Create(pricing ?? new DeliveryPricingOptions()));
        var pricingService = new DynamicDeliveryPricingService(calculator, Options.Create(new DeliveryOptions()));
        return new DeliveryQuoteService(db, pricingService, calculator);
    }

    private static DeliveryProvider AddProvider(
        CoreDbContext db,
        Guid? tenantId,
        Guid? comunaId = null,
        Guid? regionId = null,
        decimal baseFee = 1500m,
        string name = "Despacho")
    {
        var provider = new DeliveryProvider
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ComunaId = comunaId,
            RegionId = regionId,
            Name = name,
            BaseFee = baseFee,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.DeliveryProviders.Add(provider);
        db.SaveChanges();
        return provider;
    }

    [Fact]
    public async Task Quote_PartnerNotFound_ReturnsNotAvailable()
    {
        await using var db = TestDb.Create();

        var quote = await CreateQuoteService(db).GetQuoteAsync(Guid.NewGuid(), -33.45, -70.66);

        Assert.False(quote.Available);
    }

    [Fact]
    public async Task Quote_HiddenPartner_ReturnsNotAvailable()
    {
        await using var db = TestDb.Create();
        var (_, partner, _, _) = TestDb.SeedCatalog(db, stock: 1);
        partner.IsVisible = false;
        await db.SaveChangesAsync();

        var quote = await CreateQuoteService(db).GetQuoteAsync(partner.Id, -33.45, -70.66);

        Assert.False(quote.Available);
    }

    [Fact]
    public async Task Quote_NoProviderInZone_FeeDoesNotApply()
    {
        await using var db = TestDb.Create();
        var (_, partner, _, _) = TestDb.SeedCatalog(db, stock: 1);

        var quote = await CreateQuoteService(db).GetQuoteAsync(partner.Id, -33.45, -70.66);

        Assert.True(quote.Available);
        Assert.False(quote.FeeApplies);
        Assert.Equal(0m, quote.Fee);
        Assert.Null(quote.DeliveryProviderId);
    }

    [Fact]
    public async Task Quote_WithProviderAndCoords_MatchesDynamicCalculator()
    {
        await using var db = TestDb.Create();
        var (tenantId, partner, _, _) = TestDb.SeedCatalog(db, stock: 1);
        partner.Latitude = OriginLat;
        partner.Longitude = OriginLng;
        await db.SaveChangesAsync();
        var provider = AddProvider(db, tenantId);

        // ~5.3 km → excedente 3.3 → ceil 4 km (perfil diurno o nocturno según hora).
        var (lat, lng) = KmNorth(5.3);
        var quote = await CreateQuoteService(db).GetQuoteAsync(partner.Id, lat, lng);

        Assert.True(quote.Available);
        Assert.True(quote.FeeApplies);
        Assert.Equal(provider.Id, quote.DeliveryProviderId);
        Assert.False(quote.OutOfRange);
        Assert.NotNull(quote.DistanceKm);
        Assert.NotNull(quote.ProfileName);

        var expected = new DeliveryFeeCalculator(Options.Create(new DeliveryPricingOptions()))
            .Calculate(OriginLat, OriginLng, lat, lng, DateTimeOffset.UtcNow);
        Assert.Equal(expected!.TotalFee, quote.Fee);
    }

    [Fact]
    public async Task Quote_ComunaProviderWinsOverGlobal()
    {
        await using var db = TestDb.Create();
        var (tenantId, partner, _, _) = TestDb.SeedCatalog(db, stock: 1);
        partner.ComunaId = Guid.NewGuid();
        await db.SaveChangesAsync();

        AddProvider(db, tenantId, baseFee: 9000m, name: "Global");
        var comunaProvider = AddProvider(db, tenantId, comunaId: partner.ComunaId, baseFee: 1200m, name: "Comuna");

        // Sin coordenadas de destino → fallback plano = BaseFee del proveedor elegido.
        var quote = await CreateQuoteService(db).GetQuoteAsync(partner.Id, null, null);

        Assert.Equal(comunaProvider.Id, quote.DeliveryProviderId);
        Assert.Equal(1200m, quote.Fee);
        Assert.Null(quote.DistanceKm);
    }

    [Fact]
    public async Task Quote_MissingDestination_FallsBackToProviderBaseFee()
    {
        await using var db = TestDb.Create();
        var (tenantId, partner, _, _) = TestDb.SeedCatalog(db, stock: 1);
        partner.Latitude = OriginLat;
        partner.Longitude = OriginLng;
        await db.SaveChangesAsync();
        AddProvider(db, tenantId, baseFee: 2200m);

        var quote = await CreateQuoteService(db).GetQuoteAsync(partner.Id, null, null);

        Assert.True(quote.FeeApplies);
        Assert.Equal(2200m, quote.Fee);
        Assert.Null(quote.DistanceKm);
        Assert.Null(quote.ProfileName);
    }

    [Fact]
    public async Task Quote_BeyondCoverage_ReportsOutOfRangeWithoutThrowing()
    {
        await using var db = TestDb.Create();
        var (tenantId, partner, _, _) = TestDb.SeedCatalog(db, stock: 1);
        partner.Latitude = OriginLat;
        partner.Longitude = OriginLng;
        await db.SaveChangesAsync();
        AddProvider(db, tenantId);

        var (lat, lng) = KmNorth(20);
        var quote = await CreateQuoteService(db, new DeliveryPricingOptions { MaxDistanceKm = 15 })
            .GetQuoteAsync(partner.Id, lat, lng);

        Assert.True(quote.Available);
        Assert.True(quote.OutOfRange);
        Assert.Equal(15d, quote.MaxDistanceKm);
        Assert.NotNull(quote.DistanceKm);
    }

    // Paridad cotización == cobro: el fee mostrado en el checkout debe ser el
    // mismo que OrderCheckoutService persiste en la orden.
    [Fact]
    public async Task Quote_Fee_MatchesOrderDeliveryFee()
    {
        await using var db = TestDb.Create();
        var (tenantId, partner, customer, product) = TestDb.SeedCatalog(db, stock: 5, price: 1000m);
        partner.Latitude = OriginLat;
        partner.Longitude = OriginLng;
        await db.SaveChangesAsync();
        var provider = AddProvider(db, tenantId);

        var pricing = new DeliveryPricingOptions();
        var calculator = new DeliveryFeeCalculator(Options.Create(pricing));
        var pricingService = new DynamicDeliveryPricingService(calculator, Options.Create(new DeliveryOptions()));

        var (lat, lng) = KmNorth(4.2);
        var quote = await new DeliveryQuoteService(db, pricingService, calculator)
            .GetQuoteAsync(partner.Id, lat, lng);

        var checkout = new OrderCheckoutService(
            db,
            new RecordingOrderNotificationService(),
            TestDb.CreateInventoryService(db),
            new GuestCustomerService(db),
            pricingService);

        var result = await checkout.CreateOrderAsync(
            tenantId,
            customer.Id,
            new OrderCreateRequest(
                partner.Id,
                customer.Id,
                DeliveryFee: 0m,
                Currency: "CLP",
                Items: [new OrderItemCreateRequest(product.Id, 1, product.Price)],
                DeliveryProviderId: quote.DeliveryProviderId,
                DestinationLat: lat,
                DestinationLng: lng));

        Assert.True(result.Success);
        Assert.Equal(provider.Id, quote.DeliveryProviderId);
        Assert.Equal(quote.Fee, result.Order!.DeliveryFee);
        Assert.Equal(1000m + quote.Fee, result.Order.TotalAmount);
    }
}
