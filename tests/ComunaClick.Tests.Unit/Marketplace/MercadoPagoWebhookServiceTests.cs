using System.Text;
using ComunaClick.Api.Modules.Marketplace;
using ComunaClick.Api.Persistence;
using ComunaClick.Tests.Unit.Catalog;
using ComunaClick.Api.Persistence.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Xunit;

namespace ComunaClick.Tests.Unit.Marketplace;

public sealed class MercadoPagoWebhookServiceTests
{
    [Fact]
    public async Task HandleAsync_WhenProcessedWebhookAlreadyExists_IsIdempotent()
    {
        await using var db = CreateDbContext();
        var existing = new WebhookEvent
        {
            Topic = "payment",
            Action = "payment.updated",
            ResourceId = "123456",
            PayloadJson = "{}",
            SignatureValid = true,
            Processed = true,
            ProcessedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-1)
        };
        db.WebhookEvents.Add(existing);
        await db.SaveChangesAsync();

        var options = Options.Create(new MercadoPagoMarketplaceOptions
        {
            ClientId = "client-id",
            ClientSecret = "client-secret",
            RedirectUri = "https://app.comunaclic.cl/api/mercadopago/oauth/callback",
            ApiBaseUrl = "https://api.mercadopago.com",
            WebhookSecret = "webhook-secret",
            AppBaseUrl = "https://app.comunaclic.cl",
            EncryptionKey = Convert.ToBase64String(Encoding.UTF8.GetBytes("01234567890123456789012345678901"))
        });

        var client = new MercadoPagoMarketplaceClient(new HttpClient(new StubHttpMessageHandler()), options);
        var sellerService = new SellerMarketplaceService(db, new HttpContextAccessor(), new MemoryCache(new MemoryCacheOptions()), options);
        var auditService = new MarketplaceAuditService(db);
        var metrics = new MarketplaceMetricsService();
        var oauthService = new MercadoPagoOAuthService(db, client, new AesSecretProtector(options), sellerService, auditService, options, metrics);
        var service = new MercadoPagoWebhookService(db, client, oauthService, auditService, metrics, new StubProductInventoryService());

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["x-request-id"] = "req-1";
        httpContext.Request.Headers["x-signature"] = "ts=1710000000,v1=invalid";

        var result = await service.HandleAsync(
            httpContext.Request,
            """
            {
              "type": "payment",
              "action": "payment.updated",
              "data": { "id": "123456" }
            }
            """,
            CancellationToken.None);

        Assert.False(result.Processed);
        Assert.False(result.SignatureValid);
        Assert.Equal(2, await db.WebhookEvents.CountAsync());
        Assert.Equal(0, await db.PaymentStatusHistory.CountAsync());
        Assert.Equal("Invalid Mercado Pago webhook signature.", result.ErrorMessage);
    }

    [Fact]
    public void OAuthState_IsSignedAndSingleUse()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var options = Options.Create(new MercadoPagoMarketplaceOptions
        {
            ClientId = "client-id",
            ClientSecret = "client-secret",
            EncryptionKey = Convert.ToBase64String(Encoding.UTF8.GetBytes("01234567890123456789012345678901"))
        });

        using var db = CreateDbContext();
        var sellerService = new SellerMarketplaceService(db, new HttpContextAccessor(), cache, options);
        var sellerId = Guid.NewGuid();

        var state = sellerService.CreateOAuthState(sellerId);

        Assert.True(sellerService.TryReadOAuthState(state, out var parsedSellerId));
        Assert.Equal(sellerId, parsedSellerId);
        Assert.False(sellerService.TryReadOAuthState(state, out _));
    }

    private static CoreDbContext CreateDbContext()
    {
        var tenantContext = new TenantContext();
        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase($"marketplace-tests-{Guid.NewGuid():N}")
            .Options;
        return new CoreDbContext(options, tenantContext);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException($"Unexpected HTTP call in unit test: {request.Method} {request.RequestUri}");
        }
    }
}
