using ComunaClick.Api.Modules.Marketplace.Contracts;
using ComunaClick.Api.Persistence;
using ComunaClick.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ComunaClick.Api.Modules.Marketplace;

public sealed class MercadoPagoOAuthService
{
    private readonly CoreDbContext _db;
    private readonly MercadoPagoMarketplaceClient _client;
    private readonly ISecretProtector _secretProtector;
    private readonly SellerMarketplaceService _sellerService;
    private readonly MarketplaceAuditService _auditService;
    private readonly MercadoPagoMarketplaceOptions _options;
    private readonly MarketplaceMetricsService _metrics;

    public MercadoPagoOAuthService(
        CoreDbContext db,
        MercadoPagoMarketplaceClient client,
        ISecretProtector secretProtector,
        SellerMarketplaceService sellerService,
        MarketplaceAuditService auditService,
        IOptions<MercadoPagoMarketplaceOptions> options,
        MarketplaceMetricsService metrics)
    {
        _db = db;
        _client = client;
        _secretProtector = secretProtector;
        _sellerService = sellerService;
        _auditService = auditService;
        _options = options.Value;
        _metrics = metrics;
    }

    public async Task<MercadoPagoOAuthStartResponse> StartAsync(Guid sellerId, CancellationToken cancellationToken)
    {
        await _sellerService.EnsureSellerAsync(sellerId, cancellationToken);
        var state = _sellerService.CreateOAuthState(sellerId);
        var url = _client.BuildOAuthAuthorizationUrl(sellerId, state);
        return new MercadoPagoOAuthStartResponse(sellerId, url, state);
    }

    public async Task<string> CompleteAsync(string code, string state, CancellationToken cancellationToken)
    {
        if (!_sellerService.TryReadOAuthState(state, out var sellerId))
        {
            throw new InvalidOperationException("Invalid Mercado Pago OAuth state.");
        }

        var seller = await _sellerService.EnsureSellerAsync(sellerId, cancellationToken);
        var token = await _client.ExchangeCodeAsync(code, cancellationToken);
        var user = await _client.GetUserAsync(token.AccessToken, cancellationToken);

        var account = await _db.SellerMercadoPagoAccounts
            .FirstOrDefaultAsync(x => x.SellerId == sellerId, cancellationToken);

        if (account is null)
        {
            account = new SellerMercadoPagoAccount
            {
                SellerId = sellerId,
                CreatedAt = DateTimeOffset.UtcNow
            };
            _db.SellerMercadoPagoAccounts.Add(account);
        }

        account.MpUserId = token.UserId?.ToString() ?? user?.Id?.ToString();
        account.AccessTokenEncrypted = _secretProtector.Protect(token.AccessToken);
        account.RefreshTokenEncrypted = string.IsNullOrWhiteSpace(token.RefreshToken)
            ? null
            : _secretProtector.Protect(token.RefreshToken);
        account.TokenExpiresAt = token.ExpiresIn.HasValue
            ? DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn.Value)
            : null;
        account.Scope = token.Scope;
        account.ConnectionStatus = "connected";
        account.ConnectedAt = DateTimeOffset.UtcNow;
        account.RevokedAt = null;
        account.UpdatedAt = DateTimeOffset.UtcNow;

        seller.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        _metrics.Increment("sellers_connected");
        await _auditService.WriteAsync(
            _sellerService.GetActor(),
            "seller.mercadopago.connected",
            nameof(SellerMercadoPagoAccount),
            sellerId.ToString(),
            new
            {
                sellerId,
                mpUserId = account.MpUserId,
                scope = account.Scope
            },
            cancellationToken);

        return $"{_options.AppBaseUrl.TrimEnd('/')}/partner/mercadopago?sellerId={sellerId}&connected=true";
    }

    public async Task DisconnectAsync(Guid sellerId, CancellationToken cancellationToken)
    {
        var account = await _db.SellerMercadoPagoAccounts.FirstOrDefaultAsync(x => x.SellerId == sellerId, cancellationToken)
            ?? throw new InvalidOperationException("Seller Mercado Pago account was not found.");

        account.ConnectionStatus = "disconnected";
        account.AccessTokenEncrypted = string.Empty;
        account.RefreshTokenEncrypted = null;
        account.TokenExpiresAt = null;
        account.RevokedAt = DateTimeOffset.UtcNow;
        account.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        await _auditService.WriteAsync(
            _sellerService.GetActor(),
            "seller.mercadopago.disconnected",
            nameof(SellerMercadoPagoAccount),
            sellerId.ToString(),
            new { sellerId },
            cancellationToken);
    }

    public async Task<string> GetSellerAccessTokenAsync(Guid sellerId, CancellationToken cancellationToken)
    {
        var account = await _db.SellerMercadoPagoAccounts
            .FirstOrDefaultAsync(x => x.SellerId == sellerId, cancellationToken)
            ?? throw new InvalidOperationException("Seller Mercado Pago account was not found.");

        if (!string.Equals(account.ConnectionStatus, "connected", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Seller Mercado Pago account is not connected.");
        }

        if (account.TokenExpiresAt.HasValue && account.TokenExpiresAt <= DateTimeOffset.UtcNow.AddMinutes(2) &&
            !string.IsNullOrWhiteSpace(account.RefreshTokenEncrypted))
        {
            var refreshed = await _client.RefreshTokenAsync(_secretProtector.Unprotect(account.RefreshTokenEncrypted), cancellationToken);
            account.AccessTokenEncrypted = _secretProtector.Protect(refreshed.AccessToken);
            account.RefreshTokenEncrypted = string.IsNullOrWhiteSpace(refreshed.RefreshToken)
                ? account.RefreshTokenEncrypted
                : _secretProtector.Protect(refreshed.RefreshToken);
            account.TokenExpiresAt = refreshed.ExpiresIn.HasValue
                ? DateTimeOffset.UtcNow.AddSeconds(refreshed.ExpiresIn.Value)
                : account.TokenExpiresAt;
            account.Scope = refreshed.Scope ?? account.Scope;
            account.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return _secretProtector.Unprotect(account.AccessTokenEncrypted);
    }
}
