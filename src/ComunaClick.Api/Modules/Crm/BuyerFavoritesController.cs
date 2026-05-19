using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ComunaClick.Api.Modules.Crm.Contracts;
using ComunaClick.Api.Persistence;
using ComunaClick.Api.Persistence.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ComunaClick.Api.Modules.Crm;

[ApiController]
[Authorize(Policy = "buyer.customer")]
[Route("v1/buyer/favorites")]
public sealed class BuyerFavoritesController : ControllerBase
{
    private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "partner",
        "product",
        "service",
        "professional"
    };

    private readonly CoreDbContext _db;
    private readonly ITenantContext _tenantContext;

    public BuyerFavoritesController(CoreDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<BuyerFavoriteItemResponse>>> List([FromQuery] string? type)
    {
        var tenantId = _tenantContext.TenantId;
        if (!tenantId.HasValue || tenantId.Value == Guid.Empty)
        {
            return BadRequest(new { message = "TenantId is required." });
        }

        var normalizedType = NormalizeType(type, allowNull: true);
        if (type is not null && normalizedType is null)
        {
            return BadRequest(new { message = "Type is not valid." });
        }

        var customer = await ResolveCustomerAsync(tenantId.Value);
        if (customer is null)
        {
            return BadRequest(new { message = "Customer context is required." });
        }

        var query = _db.BuyerFavorites.AsNoTracking()
            .Where(x => x.TenantId == tenantId.Value && x.CustomerId == customer.Id);

        if (!string.IsNullOrWhiteSpace(normalizedType))
        {
            query = query.Where(x => x.Type == normalizedType);
        }

        var favorites = await query
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        var items = await BuildResponseItemsAsync(favorites);
        return Ok(items);
    }

    [HttpPost]
    public async Task<ActionResult<BuyerFavoriteItemResponse>> Create([FromBody] BuyerFavoriteCreateRequest request)
    {
        if (request.TargetId == Guid.Empty)
        {
            return BadRequest(new { message = "TargetId is required." });
        }

        var normalizedType = NormalizeType(request.Type, allowNull: false);
        if (normalizedType is null)
        {
            return BadRequest(new { message = "Type is not valid." });
        }

        var tenantId = request.TenantId ?? _tenantContext.TenantId;
        if (!tenantId.HasValue || tenantId.Value == Guid.Empty)
        {
            return BadRequest(new { message = "TenantId is required." });
        }

        if (_tenantContext.TenantId.HasValue && _tenantContext.TenantId.Value != tenantId.Value)
        {
            return BadRequest(new { message = "TenantId does not match authenticated context." });
        }

        var customer = await ResolveCustomerAsync(tenantId.Value);
        if (customer is null)
        {
            return BadRequest(new { message = "Customer context is required." });
        }

        var target = await ResolveTargetAsync(tenantId.Value, normalizedType, request.TargetId);
        if (target is null)
        {
            return BadRequest(new { message = "Target is not available for favorites." });
        }

        var existing = await _db.BuyerFavorites
            .FirstOrDefaultAsync(x =>
                x.TenantId == tenantId.Value &&
                x.CustomerId == customer.Id &&
                x.Type == normalizedType &&
                x.TargetId == request.TargetId);

        if (existing is not null)
        {
            var existingItem = (await BuildResponseItemsAsync(new[] { existing })).First();
            return Ok(existingItem);
        }

        var favorite = new BuyerFavorite
        {
            TenantId = tenantId.Value,
            CustomerId = customer.Id,
            Type = normalizedType,
            TargetId = request.TargetId,
            PartnerId = target.PartnerId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.BuyerFavorites.Add(favorite);
        await _db.SaveChangesAsync();

        var item = (await BuildResponseItemsAsync(new[] { favorite })).First();
        return Created($"/v1/buyer/favorites/{favorite.Id}", item);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var tenantId = _tenantContext.TenantId;
        if (!tenantId.HasValue || tenantId.Value == Guid.Empty)
        {
            return BadRequest(new { message = "TenantId is required." });
        }

        var customer = await ResolveCustomerAsync(tenantId.Value);
        if (customer is null)
        {
            return BadRequest(new { message = "Customer context is required." });
        }

        var favorite = await _db.BuyerFavorites
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId.Value && x.CustomerId == customer.Id);

        if (favorite is null)
        {
            return NotFound();
        }

        _db.BuyerFavorites.Remove(favorite);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<Customer?> ResolveCustomerAsync(Guid tenantId)
    {
        var email = ResolveEmail(User);
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        return await _db.Customers
            .FirstOrDefaultAsync(x =>
                x.TenantId == tenantId &&
                x.Email != null &&
                x.Email.ToLower() == normalizedEmail);
    }

    private async Task<TargetResolution?> ResolveTargetAsync(Guid tenantId, string type, Guid targetId)
    {
        if (type == "partner")
        {
            var partner = await _db.Partners.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == targetId && x.TenantId == tenantId && x.IsVisible);
            return partner is null ? null : new TargetResolution(partner.Id);
        }

        if (type == "product")
        {
            var product = await _db.Products.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == targetId && x.TenantId == tenantId && x.IsActive);
            if (product is null)
            {
                return null;
            }

            var visiblePartner = await _db.Partners.AsNoTracking()
                .AnyAsync(x => x.Id == product.PartnerId && x.TenantId == tenantId && x.IsVisible);
            return visiblePartner ? new TargetResolution(product.PartnerId) : null;
        }

        if (type == "service")
        {
            var service = await _db.Services.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == targetId && x.TenantId == tenantId && x.IsActive);
            if (service is null)
            {
                return null;
            }

            var visiblePartner = await _db.Partners.AsNoTracking()
                .AnyAsync(x => x.Id == service.PartnerId && x.TenantId == tenantId && x.IsVisible);
            return visiblePartner ? new TargetResolution(service.PartnerId) : null;
        }

        var professional = await _db.Professionals.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == targetId && x.TenantId == tenantId && x.IsActive && x.IsVerified);
        return professional is null ? null : new TargetResolution(null);
    }

    private async Task<IReadOnlyList<BuyerFavoriteItemResponse>> BuildResponseItemsAsync(IEnumerable<BuyerFavorite> favorites)
    {
        var favoriteList = favorites.ToList();
        if (favoriteList.Count == 0)
        {
            return Array.Empty<BuyerFavoriteItemResponse>();
        }

        var partnerIds = favoriteList
            .Where(x => x.Type == "partner")
            .Select(x => x.TargetId)
            .Concat(favoriteList.Where(x => x.PartnerId.HasValue).Select(x => x.PartnerId!.Value))
            .Distinct()
            .ToList();

        var productIds = favoriteList.Where(x => x.Type == "product").Select(x => x.TargetId).Distinct().ToList();
        var serviceIds = favoriteList.Where(x => x.Type == "service").Select(x => x.TargetId).Distinct().ToList();
        var professionalIds = favoriteList.Where(x => x.Type == "professional").Select(x => x.TargetId).Distinct().ToList();

        var partners = partnerIds.Count == 0
            ? new Dictionary<Guid, Partner>()
            : await _db.Partners.AsNoTracking().Where(x => partnerIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id);

        var products = productIds.Count == 0
            ? new Dictionary<Guid, Product>()
            : await _db.Products.AsNoTracking().Where(x => productIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id);

        var services = serviceIds.Count == 0
            ? new Dictionary<Guid, Service>()
            : await _db.Services.AsNoTracking().Where(x => serviceIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id);

        var professionals = professionalIds.Count == 0
            ? new Dictionary<Guid, Professional>()
            : await _db.Professionals.AsNoTracking().Where(x => professionalIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id);

        var items = new List<BuyerFavoriteItemResponse>(favoriteList.Count);
        foreach (var favorite in favoriteList)
        {
            if (favorite.Type == "partner")
            {
                partners.TryGetValue(favorite.TargetId, out var partner);
                items.Add(new BuyerFavoriteItemResponse(
                    favorite.Id,
                    favorite.Type,
                    favorite.TargetId,
                    favorite.CustomerId,
                    partner?.Id,
                    partner?.Name ?? "No disponible",
                    partner?.Address ?? partner?.Email ?? partner?.Phone,
                    null,
                    null,
                    null,
                    null,
                    partner is not null && partner.IsVisible,
                    favorite.CreatedAt));
                continue;
            }

            if (favorite.Type == "product")
            {
                products.TryGetValue(favorite.TargetId, out var product);
                items.Add(new BuyerFavoriteItemResponse(
                    favorite.Id,
                    favorite.Type,
                    favorite.TargetId,
                    favorite.CustomerId,
                    product?.PartnerId,
                    product?.Name ?? "No disponible",
                    product?.Description,
                    product?.Category,
                    product?.Price,
                    product?.Currency,
                    null,
                    product is not null && product.IsActive,
                    favorite.CreatedAt));
                continue;
            }

            if (favorite.Type == "service")
            {
                services.TryGetValue(favorite.TargetId, out var service);
                items.Add(new BuyerFavoriteItemResponse(
                    favorite.Id,
                    favorite.Type,
                    favorite.TargetId,
                    favorite.CustomerId,
                    service?.PartnerId,
                    service?.Name ?? "No disponible",
                    service?.Description,
                    service?.Category,
                    service?.Price,
                    service?.Currency,
                    null,
                    service is not null && service.IsActive,
                    favorite.CreatedAt));
                continue;
            }

            professionals.TryGetValue(favorite.TargetId, out var professional);
            items.Add(new BuyerFavoriteItemResponse(
                favorite.Id,
                favorite.Type,
                favorite.TargetId,
                favorite.CustomerId,
                favorite.PartnerId,
                professional?.Name ?? "No disponible",
                professional?.Bio,
                professional?.Specialty,
                null,
                null,
                null,
                professional is not null && professional.IsActive && professional.IsVerified,
                favorite.CreatedAt));
        }

        return items;
    }

    private static string? ResolveEmail(ClaimsPrincipal user)
    {
        return user.FindFirst(ClaimTypes.Email)?.Value
            ?? user.FindFirst(JwtRegisteredClaimNames.Email)?.Value
            ?? user.FindFirst("email")?.Value;
    }

    private static string? NormalizeType(string? value, bool allowNull)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return allowNull ? null : null;
        }

        var normalized = value.Trim().ToLowerInvariant();
        return AllowedTypes.Contains(normalized) ? normalized : null;
    }

    private sealed record TargetResolution(Guid? PartnerId);
}
