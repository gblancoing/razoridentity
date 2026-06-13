using System.Net;
using System.Text;
using ComunaClick.Api.Modules.Delivery;
using ComunaClick.Api.Modules.Marketplace;
using ComunaClick.Api.Persistence;
using ComunaClick.Api.Persistence.Entities;
using ComunaClick.Tests.Unit.TestSupport;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Xunit;

namespace ComunaClick.Tests.Unit.Delivery;

/// <summary>
/// Pago comercio → repartidor: link de Checkout Pro con collector = courier y
/// liquidación automática del slip al confirmarse el pago.
/// </summary>
public sealed class DeliverySettlementPaymentTests
{
    private static IOptions<MercadoPagoMarketplaceOptions> MpOptions() => Options.Create(new MercadoPagoMarketplaceOptions
    {
        ClientId = "client-id",
        ClientSecret = "client-secret",
        ApiBaseUrl = "https://api.mercadopago.test",
        AppBaseUrl = "https://app.comunaclic.cl",
        EncryptionKey = Convert.ToBase64String(Encoding.UTF8.GetBytes("01234567890123456789012345678901"))
    });

    private static DeliverySettlementPaymentService CreateService(CoreDbContext db, HttpMessageHandler? handler = null)
    {
        var options = MpOptions();
        var client = new MercadoPagoMarketplaceClient(new HttpClient(handler ?? new ThrowingHandler()), options);
        var sellerService = new SellerMarketplaceService(db, new HttpContextAccessor(), new MemoryCache(new MemoryCacheOptions()), options);
        var oauth = new MercadoPagoOAuthService(db, client, new AesSecretProtector(options), sellerService, new MarketplaceAuditService(db), options, new MarketplaceMetricsService());
        return new DeliverySettlementPaymentService(db, oauth, client, new CourierPayeeService(db), options);
    }

    private static (DeliverySettlement Settlement, Order Order, Courier Courier) SeedSettlement(
        CoreDbContext db,
        string deliveryStatus = "delivered",
        string settlementStatus = "pending",
        bool withMpAccount = true)
    {
        var tenantId = Guid.NewGuid();
        var partnerId = Guid.NewGuid();
        var courier = new Courier
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PartnerId = partnerId,
            Name = "Repartidor Pago",
            Phone = "+56900000001",
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
            DeliveryFee = 3000m,
            TotalAmount = 13000m,
            GrossAmount = 13000m,
            NetAmount = 13000m,
            Currency = "CLP",
            DeliveryType = DeliveryTypes.Delivery,
            DeliveryStatus = deliveryStatus,
            CourierId = courier.Id,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        var settlement = new DeliverySettlement
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OrderId = order.Id,
            PartnerId = partnerId,
            CourierId = courier.Id,
            GrossAmount = 3000m,
            PlatformFeeAmount = 300m,
            NetToCourierAmount = 2700m,
            Currency = "CLP",
            Status = settlementStatus,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.Couriers.Add(courier);
        db.Orders.Add(order);
        db.DeliverySettlements.Add(settlement);

        if (withMpAccount)
        {
            // Cuenta MP del courier conectada: fila Seller con Id = courier.Id.
            var protector = new AesSecretProtector(MpOptions());
            db.Sellers.Add(new Seller { Id = courier.Id, TenantId = tenantId, Name = courier.Name, Email = "c@test.cl" });
            db.SellerMercadoPagoAccounts.Add(new SellerMercadoPagoAccount
            {
                Id = Guid.NewGuid(),
                SellerId = courier.Id,
                ConnectionStatus = "connected",
                AccessTokenEncrypted = protector.Protect("courier-access-token"),
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }

        db.SaveChanges();
        return (settlement, order, courier);
    }

    [Fact]
    public async Task PaymentLink_RequiresDeliveredOrder()
    {
        await using var db = TestDb.Create();
        var (settlement, _, _) = SeedSettlement(db, deliveryStatus: "in_transit");

        var (link, error) = await CreateService(db).CreatePaymentLinkAsync(settlement.Id, settlement.PartnerId);

        Assert.Null(link);
        Assert.Contains("entregado", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PaymentLink_RejectsSettledSlip()
    {
        await using var db = TestDb.Create();
        var (settlement, _, _) = SeedSettlement(db, settlementStatus: "settled");

        var (link, error) = await CreateService(db).CreatePaymentLinkAsync(settlement.Id, settlement.PartnerId);

        Assert.Null(link);
        Assert.Contains("ya fue pagado", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PaymentLink_RequiresCourierMpAccount()
    {
        await using var db = TestDb.Create();
        var (settlement, _, _) = SeedSettlement(db, withMpAccount: false);

        var (link, error) = await CreateService(db).CreatePaymentLinkAsync(settlement.Id, settlement.PartnerId);

        Assert.Null(link);
        Assert.Contains("MercadoPago", error);
    }

    [Fact]
    public async Task PaymentLink_CreatesPreferenceAndMarksProcessing()
    {
        await using var db = TestDb.Create();
        var (settlement, _, courier) = SeedSettlement(db);
        var handler = new PreferenceHandler();

        var (link, error) = await CreateService(db, handler).CreatePaymentLinkAsync(settlement.Id, settlement.PartnerId);

        Assert.Null(error);
        Assert.Equal("https://mp.test/init/abc", link!.InitPoint);

        var stored = db.DeliverySettlements.Single();
        Assert.Equal("processing", stored.Status);
        Assert.NotNull(stored.PaymentId);

        var payment = db.Payments.Single();
        Assert.Equal(courier.Id, payment.SellerId); // collector = repartidor
        Assert.Null(payment.OrderId);
        Assert.Equal($"cc-delivery-{settlement.Id:N}", payment.ExternalReference);
        Assert.Equal(3000m, payment.Amount); // bruto del envío
    }

    [Fact]
    public async Task PaymentLink_IsIdempotentWhilePending()
    {
        await using var db = TestDb.Create();
        var (settlement, _, _) = SeedSettlement(db);
        var handler = new PreferenceHandler();
        var service = CreateService(db, handler);

        var (first, _) = await service.CreatePaymentLinkAsync(settlement.Id, settlement.PartnerId);
        var (second, _) = await service.CreatePaymentLinkAsync(settlement.Id, settlement.PartnerId);

        Assert.Equal(first!.InitPoint, second!.InitPoint);
        Assert.Single(db.Payments); // no duplica el pago
        Assert.Equal(1, handler.PreferenceCalls); // el segundo llamado reutiliza el link
    }

    [Fact]
    public async Task CourierPaymentApproved_SettlesSlipWithRealMpFee()
    {
        await using var db = TestDb.Create();
        var (settlement, _, courier) = SeedSettlement(db);
        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            TenantId = settlement.TenantId,
            SellerId = courier.Id,
            ExternalReference = $"cc-delivery-{settlement.Id:N}",
            Amount = 3000m,
            Status = "approved",
            MercadoPagoPaymentId = "987654"
        };
        settlement.Status = "processing";
        db.Payments.Add(payment);
        await db.SaveChangesAsync();

        // Fee MP real de ESTA transacción ($113, impar a propósito).
        await TestDb.CreateSettlementService(db).ApplyCourierPaymentOutcomeAsync(payment, realMpFee: 113m);

        var stored = db.DeliverySettlements.Single();
        Assert.Equal("settled", stored.Status);
        Assert.NotNull(stored.SettledAt);
        Assert.Equal(113m, stored.MercadoPagoFeeAmount);
        // bruto = feeMP + feeCC + neto, exacto.
        Assert.Equal(stored.GrossAmount, stored.MercadoPagoFeeAmount!.Value + stored.PlatformFeeAmount + stored.NetToCourierAmount);
        Assert.Equal(2587m, stored.NetToCourierAmount);
        Assert.Contains("987654", stored.Notes);
    }

    [Fact]
    public async Task CourierPaymentRejected_ReturnsSlipToPending()
    {
        await using var db = TestDb.Create();
        var (settlement, _, courier) = SeedSettlement(db, settlementStatus: "processing");
        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            TenantId = settlement.TenantId,
            SellerId = courier.Id,
            ExternalReference = $"cc-delivery-{settlement.Id:N}",
            Amount = 3000m,
            Status = "rejected"
        };
        db.Payments.Add(payment);
        await db.SaveChangesAsync();

        await TestDb.CreateSettlementService(db).ApplyCourierPaymentOutcomeAsync(payment, realMpFee: null);

        Assert.Equal("pending", db.DeliverySettlements.Single().Status);
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new InvalidOperationException($"Unexpected HTTP call: {request.Method} {request.RequestUri}");
    }

    /// <summary>Stub del API MP: responde la creación de preference de Checkout Pro.</summary>
    private sealed class PreferenceHandler : HttpMessageHandler
    {
        public int PreferenceCalls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Post && request.RequestUri!.AbsolutePath.Contains("preferences"))
            {
                PreferenceCalls++;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created)
                {
                    Content = new StringContent(
                        """{"id":"pref-1","init_point":"https://mp.test/init/abc","sandbox_init_point":null}""",
                        Encoding.UTF8,
                        "application/json")
                });
            }

            throw new InvalidOperationException($"Unexpected HTTP call: {request.Method} {request.RequestUri}");
        }
    }
}
