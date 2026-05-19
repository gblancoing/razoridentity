using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ComunaClick.Api.Persistence;
using ComunaClick.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace ComunaClick.Api.Modules.Marketplace;

public sealed class SellerMarketplaceService
{
    private const int OAuthStateLifetimeMinutes = 10;
    private readonly CoreDbContext _db;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IMemoryCache _memoryCache;
    private readonly MercadoPagoMarketplaceOptions _options;

    public SellerMarketplaceService(
        CoreDbContext db,
        IHttpContextAccessor httpContextAccessor,
        IMemoryCache memoryCache,
        IOptions<MercadoPagoMarketplaceOptions> options)
    {
        _db = db;
        _httpContextAccessor = httpContextAccessor;
        _memoryCache = memoryCache;
        _options = options.Value;
    }

    public async Task<Seller> EnsureSellerAsync(Guid sellerId, CancellationToken cancellationToken)
    {
        var seller = await _db.Sellers
            .Include(x => x.MercadoPagoAccount)
            .Include(x => x.FeeConfiguration)
            .FirstOrDefaultAsync(x => x.Id == sellerId, cancellationToken);

        if (seller is not null)
        {
            return seller;
        }

        var partner = await _db.Partners.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == sellerId, cancellationToken)
            ?? throw new InvalidOperationException("Seller partner was not found.");

        seller = new Seller
        {
            Id = partner.Id,
            TenantId = partner.TenantId,
            Name = partner.Name,
            Email = partner.Email ?? $"seller-{partner.Id:N}@comunaclic.cl",
            TaxId = partner.Rut,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _db.Sellers.Add(seller);
        await _db.SaveChangesAsync(cancellationToken);
        return seller;
    }

    public Task<bool> UserCanManageSellerAsync(Guid sellerId, CancellationToken cancellationToken)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var user = httpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return Task.FromResult(false);
        }

        if (user.IsInRole("platform_admin") || user.IsInRole("tenant_admin"))
        {
            return Task.FromResult(true);
        }

        var partnerIdClaim = user.FindFirst("partner_id")?.Value
            ?? user.FindFirst("partnerId")?.Value
            ?? user.FindFirst(ComunaClick.Common.Auth.AuthConstants.ClaimPartnerId)?.Value;

        return Task.FromResult(Guid.TryParse(partnerIdClaim, out var partnerId) && partnerId == sellerId);
    }

    public string GetActor()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return "anonymous";
        }

        var email = user.FindFirst(ClaimTypes.Email)?.Value
            ?? user.FindFirst("email")?.Value;
        var sub = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst("sub")?.Value
            ?? "unknown";

        return string.IsNullOrWhiteSpace(email) ? sub : $"{email} ({sub})";
    }

    public string CreateOAuthState(Guid sellerId)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var nonce = Guid.NewGuid().ToString("N");
        var payload = $"{sellerId:N}:{timestamp}:{nonce}";
        var signature = ComputeStateSignature(payload);
        _memoryCache.Set(GetOAuthStateCacheKey(nonce), sellerId, TimeSpan.FromMinutes(OAuthStateLifetimeMinutes));
        return Convert.ToBase64String(Encoding.UTF8.GetBytes($"{payload}:{signature}"));
    }

    public bool TryReadOAuthState(string state, out Guid sellerId)
    {
        sellerId = Guid.Empty;
        if (string.IsNullOrWhiteSpace(state))
        {
            return false;
        }

        try
        {
            var raw = Encoding.UTF8.GetString(Convert.FromBase64String(state));
            var parts = raw.Split(':', 4);
            if (parts.Length != 4 ||
                !Guid.TryParseExact(parts[0], "N", out sellerId) ||
                !long.TryParse(parts[1], out var issuedAtUnix) ||
                string.IsNullOrWhiteSpace(parts[2]) ||
                string.IsNullOrWhiteSpace(parts[3]))
            {
                return false;
            }

            var payload = $"{parts[0]}:{parts[1]}:{parts[2]}";
            var expectedSignature = ComputeStateSignature(payload);
            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(expectedSignature),
                    Encoding.UTF8.GetBytes(parts[3])))
            {
                return false;
            }

            var issuedAt = DateTimeOffset.FromUnixTimeSeconds(issuedAtUnix);
            if (issuedAt < DateTimeOffset.UtcNow.AddMinutes(-OAuthStateLifetimeMinutes))
            {
                _memoryCache.Remove(GetOAuthStateCacheKey(parts[2]));
                return false;
            }

            var cacheKey = GetOAuthStateCacheKey(parts[2]);
            if (!_memoryCache.TryGetValue<Guid>(cacheKey, out var cachedSellerId) || cachedSellerId != sellerId)
            {
                return false;
            }

            _memoryCache.Remove(cacheKey);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private string ComputeStateSignature(string payload)
    {
        using var hmac = new HMACSHA256(GetStateSigningKeyBytes());
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }

    private byte[] GetStateSigningKeyBytes()
    {
        var rawKey = !string.IsNullOrWhiteSpace(_options.EncryptionKey)
            ? _options.EncryptionKey
            : _options.ClientSecret;

        if (string.IsNullOrWhiteSpace(rawKey))
        {
            throw new InvalidOperationException("Marketplace signing key is not configured.");
        }

        try
        {
            return Convert.FromBase64String(rawKey);
        }
        catch (FormatException)
        {
            return Encoding.UTF8.GetBytes(rawKey);
        }
    }

    private static string GetOAuthStateCacheKey(string nonce)
        => $"mercadopago-oauth-state:{nonce}";
}
