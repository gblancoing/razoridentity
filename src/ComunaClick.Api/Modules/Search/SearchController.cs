using ComunaClick.Api.Geo;
using ComunaClick.Api.Modules.Catalog;
using ComunaClick.Api.Modules.Search.Contracts;
using ComunaClick.Api.Persistence;
using ComunaClick.Api.Persistence.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace ComunaClick.Api.Modules.Search;

[ApiController]
[AllowAnonymous]
[EnableRateLimiting("public-read")]
[Route("v1/search")]
public sealed class SearchController : ControllerBase
{
    private const int DefaultRadiusKm = 30;
    private const int MaxFetchWhenGeo = 200;

    private readonly CoreDbContext _db;

    public SearchController(CoreDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<SearchResultItem>>> Search(
        [FromQuery] string? query,
        [FromQuery] string? type,
        [FromQuery] Guid? countryId,
        [FromQuery] Guid? regionId,
        [FromQuery] Guid? comunaId,
        [FromQuery] double? latitude,
        [FromQuery] double? longitude,
        [FromQuery] double? radiusKm,
        [FromQuery] int? limit)
    {
        var take = Math.Clamp(limit ?? 20, 1, 100);
        var hasQuery = !string.IsNullOrWhiteSpace(query);
        var filter = hasQuery ? $"%{query!.Trim()}%" : null;
        var useGeo = latitude.HasValue && longitude.HasValue && GeoDistance.IsValidCoordinate(latitude.Value, longitude.Value);
        var maxRadius = Math.Clamp(radiusKm ?? DefaultRadiusKm, 1, 200);
        var originLat = latitude ?? 0;
        var originLng = longitude ?? 0;

        var comunasById = await _db.Comunas.AsNoTracking()
            .Where(x => x.IsActive && x.Latitude != null && x.Longitude != null)
            .ToDictionaryAsync(x => x.Id);

        var results = new List<SearchResultItem>();

        if (string.IsNullOrWhiteSpace(type) || type.Equals("partners", StringComparison.OrdinalIgnoreCase))
        {
            results.AddRange(await SearchPartnersInternalAsync(
                hasQuery, filter, countryId, regionId, comunaId, useGeo, originLat, originLng, maxRadius, comunasById, take));
        }

        if (string.IsNullOrWhiteSpace(type) || type.Equals("products", StringComparison.OrdinalIgnoreCase))
        {
            results.AddRange(await SearchProductsInternalAsync(
                hasQuery, filter, countryId, regionId, comunaId, useGeo, originLat, originLng, maxRadius, comunasById, take));
        }

        if (string.IsNullOrWhiteSpace(type) || type.Equals("services", StringComparison.OrdinalIgnoreCase))
        {
            results.AddRange(await SearchServicesInternalAsync(
                hasQuery, filter, countryId, regionId, comunaId, useGeo, originLat, originLng, maxRadius, comunasById, take));
        }

        if (string.IsNullOrWhiteSpace(type) || type.Equals("professionals", StringComparison.OrdinalIgnoreCase))
        {
            results.AddRange(await SearchProfessionalsInternalAsync(
                hasQuery, filter, countryId, regionId, comunaId, useGeo, originLat, originLng, maxRadius, comunasById, take));
        }

        if (useGeo)
        {
            return Ok(results
                .OrderBy(x => x.DistanceKm ?? double.MaxValue)
                .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .Take(take));
        }

        return Ok(results.Take(take));
    }

    [HttpGet("partners")]
    public async Task<ActionResult<IEnumerable<SearchResultItem>>> SearchPartners(
        [FromQuery] string? query,
        [FromQuery] Guid? countryId,
        [FromQuery] Guid? regionId,
        [FromQuery] Guid? comunaId,
        [FromQuery] double? latitude,
        [FromQuery] double? longitude,
        [FromQuery] double? radiusKm,
        [FromQuery] int? limit)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Ok(Array.Empty<SearchResultItem>());
        }

        var take = Math.Clamp(limit ?? 20, 1, 100);
        var useGeo = latitude.HasValue && longitude.HasValue && GeoDistance.IsValidCoordinate(latitude.Value, longitude.Value);
        var comunasById = await _db.Comunas.AsNoTracking()
            .Where(x => x.IsActive && x.Latitude != null && x.Longitude != null)
            .ToDictionaryAsync(x => x.Id);

        var items = await SearchPartnersInternalAsync(
            true, $"%{query.Trim()}%", countryId, regionId, comunaId,
            useGeo, latitude ?? 0, longitude ?? 0, Math.Clamp(radiusKm ?? DefaultRadiusKm, 1, 200),
            comunasById, take);

        return Ok(items);
    }

    [HttpGet("professionals")]
    public async Task<ActionResult<IEnumerable<SearchResultItem>>> SearchProfessionals(
        [FromQuery] string? query,
        [FromQuery] Guid? countryId,
        [FromQuery] Guid? regionId,
        [FromQuery] Guid? comunaId,
        [FromQuery] bool? verified,
        [FromQuery] double? latitude,
        [FromQuery] double? longitude,
        [FromQuery] double? radiusKm,
        [FromQuery] int? limit)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Ok(Array.Empty<SearchResultItem>());
        }

        var take = Math.Clamp(limit ?? 20, 1, 100);
        var useGeo = latitude.HasValue && longitude.HasValue && GeoDistance.IsValidCoordinate(latitude.Value, longitude.Value);
        var comunasById = await _db.Comunas.AsNoTracking()
            .Where(x => x.IsActive && x.Latitude != null && x.Longitude != null)
            .ToDictionaryAsync(x => x.Id);

        var items = await SearchProfessionalsInternalAsync(
            true, $"%{query.Trim()}%", countryId, regionId, comunaId,
            useGeo, latitude ?? 0, longitude ?? 0, Math.Clamp(radiusKm ?? DefaultRadiusKm, 1, 200),
            comunasById, take, verified);

        return Ok(items);
    }

    private async Task<List<SearchResultItem>> SearchPartnersInternalAsync(
        bool hasQuery,
        string? filter,
        Guid? countryId,
        Guid? regionId,
        Guid? comunaId,
        bool useGeo,
        double originLat,
        double originLng,
        double maxRadiusKm,
        IReadOnlyDictionary<Guid, Comuna> comunasById,
        int take)
    {
        var partnersQuery = _db.Partners.AsNoTracking()
            .Where(x => x.IsVisible)
            .Where(x => !countryId.HasValue || x.CountryId == countryId.Value)
            .Where(x => !regionId.HasValue || x.RegionId == regionId.Value)
            .Where(x => !comunaId.HasValue || x.ComunaId == comunaId.Value)
            .Where(x => x.Type != "A"
                        || _db.Products.Any(p => p.PartnerId == x.Id && p.IsActive)
                        || (x.OffersServices && _db.Services.Any(s => s.PartnerId == x.Id && s.IsActive && s.DeletedAt == null)))
            .Where(x => x.Type != "B" || _db.Services.Any(s => s.PartnerId == x.Id && s.IsActive))
            .Where(x => x.Type != "C" || _db.Professionals.Any(p => (p.ComunaId == x.ComunaId || (p.ComunaId == null && p.TenantId == x.TenantId)) && p.IsActive && p.IsVerified));

        if (hasQuery)
        {
            partnersQuery = partnersQuery.Where(x => EF.Functions.ILike(x.Name, filter!));
        }

        var fetchLimit = useGeo ? MaxFetchWhenGeo : take;
        var partners = await partnersQuery.OrderBy(x => x.Name).Take(fetchLimit).ToListAsync();

        var items = new List<SearchResultItem>();
        foreach (var partner in partners)
        {
            var coords = SearchGeoResolver.ResolvePartnerCoordinates(partner, comunasById);
            double? distanceKm = null;
            if (useGeo)
            {
                distanceKm = SearchGeoResolver.DistanceKmFrom(originLat, originLng, coords);
                if (!SearchGeoResolver.WithinRadius(distanceKm, maxRadiusKm))
                {
                    continue;
                }
            }

            items.Add(BuildSearchResult(
                "partner",
                partner.Id,
                partner.Name,
                partner.Type,
                partner.Id,
                null,
                null,
                PartnerCtaLabel(partner.Type),
                $"/buyer/detail/partner/{partner.Id}",
                coords,
                distanceKm,
                partner.LogoUrl));
        }

        return OrderAndTake(items, useGeo, take);
    }

    private async Task<List<SearchResultItem>> SearchProductsInternalAsync(
        bool hasQuery,
        string? filter,
        Guid? countryId,
        Guid? regionId,
        Guid? comunaId,
        bool useGeo,
        double originLat,
        double originLng,
        double maxRadiusKm,
        IReadOnlyDictionary<Guid, Comuna> comunasById,
        int take)
    {
        var productsQuery = _db.Products.AsNoTracking()
            .Where(x => x.IsActive)
            .Where(x => !countryId.HasValue || x.CountryId == countryId.Value)
            .Where(x => !regionId.HasValue || x.RegionId == regionId.Value)
            .Where(x => !comunaId.HasValue || x.ComunaId == comunaId.Value)
            .Where(x => _db.Partners.Any(p => p.Id == x.PartnerId && p.IsVisible));

        if (hasQuery)
        {
            productsQuery = productsQuery.Where(x => EF.Functions.ILike(x.Name, filter!));
        }

        var fetchLimit = useGeo ? MaxFetchWhenGeo : take;
        var products = await productsQuery.OrderBy(x => x.Name).Take(fetchLimit).ToListAsync();
        if (products.Count > 0)
        {
            await ProductMediaSync.EnrichManyAsync(_db, products, HttpContext.RequestAborted);
        }

        Dictionary<Guid, Partner>? partnersById = null;
        if (products.Count > 0)
        {
            var partnerIds = products.Select(x => x.PartnerId).Distinct().ToList();
            partnersById = await _db.Partners.AsNoTracking()
                .Where(x => partnerIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id);
        }

        var items = new List<SearchResultItem>();
        foreach (var product in products)
        {
            SearchGeoResolver.Coordinates? coords = null;
            double? distanceKm = null;
            Partner? productPartner = null;
            if (partnersById is not null && partnersById.TryGetValue(product.PartnerId, out productPartner))
            {
                coords = SearchGeoResolver.ResolveProductCoordinates(product, productPartner, comunasById);
            }

            if (useGeo)
            {
                distanceKm = SearchGeoResolver.DistanceKmFrom(originLat, originLng, coords);
                if (!SearchGeoResolver.WithinRadius(distanceKm, maxRadiusKm))
                {
                    continue;
                }
            }

            var coverImage = product.ImageUrls.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
                ?? product.ImageUrl;

            items.Add(BuildSearchResult(
                "product",
                product.Id,
                product.Name,
                product.Category,
                product.PartnerId,
                product.Price,
                product.Currency,
                "Comprar",
                $"/buyer/detail/product/{product.Id}",
                coords,
                distanceKm,
                productPartner?.LogoUrl,
                coverImage,
                product.ImageUrls));
        }

        return OrderAndTake(items, useGeo, take);
    }

    private async Task<List<SearchResultItem>> SearchServicesInternalAsync(
        bool hasQuery,
        string? filter,
        Guid? countryId,
        Guid? regionId,
        Guid? comunaId,
        bool useGeo,
        double originLat,
        double originLng,
        double maxRadiusKm,
        IReadOnlyDictionary<Guid, Comuna> comunasById,
        int take)
    {
        var servicesQuery = _db.Services.AsNoTracking()
            .Where(x => x.IsActive && x.DeletedAt == null)
            .Where(x => !countryId.HasValue || x.CountryId == countryId.Value)
            .Where(x => !regionId.HasValue || x.RegionId == regionId.Value)
            .Where(x => !comunaId.HasValue || x.ComunaId == comunaId.Value)
            .Where(x => _db.Partners.Any(p => p.Id == x.PartnerId && p.IsVisible));

        if (hasQuery)
        {
            servicesQuery = servicesQuery.Where(x => EF.Functions.ILike(x.Name, filter!));
        }

        var fetchLimit = useGeo ? MaxFetchWhenGeo : take;
        var services = await servicesQuery.OrderBy(x => x.Name).Take(fetchLimit).ToListAsync();

        Dictionary<Guid, Partner>? partnersById = null;
        Dictionary<Guid, IReadOnlyList<string>> serviceImagesByService = new();
        if (services.Count > 0)
        {
            var partnerIds = services.Select(x => x.PartnerId).Distinct().ToList();
            partnersById = await _db.Partners.AsNoTracking()
                .Where(x => partnerIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id);

            var serviceIds = services.Select(x => x.Id).ToList();
            var imageRows = await _db.ServiceImages.AsNoTracking()
                .Where(x => serviceIds.Contains(x.ServiceId))
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.CreatedAt)
                .Select(x => new { x.ServiceId, x.Url })
                .ToListAsync();

            serviceImagesByService = imageRows
                .GroupBy(x => x.ServiceId)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(x => x.Url).ToList());
        }

        var items = new List<SearchResultItem>();
        foreach (var service in services)
        {
            SearchGeoResolver.Coordinates? coords = null;
            if (partnersById is not null && partnersById.TryGetValue(service.PartnerId, out var partner))
            {
                coords = SearchGeoResolver.ResolveServiceCoordinates(service, partner, comunasById);
            }

            double? distanceKm = null;
            if (useGeo)
            {
                distanceKm = SearchGeoResolver.DistanceKmFrom(originLat, originLng, coords);
                if (!SearchGeoResolver.WithinRadius(distanceKm, maxRadiusKm))
                {
                    continue;
                }
            }

            string? serviceLogo = null;
            Partner? servicePartner = null;
            if (partnersById is not null && partnersById.TryGetValue(service.PartnerId, out servicePartner))
            {
                serviceLogo = servicePartner.LogoUrl;
            }

            var serviceCover = serviceImagesByService.TryGetValue(service.Id, out var urls) && urls.Count > 0
                ? urls[0]
                : service.ImageUrl;

            items.Add(BuildSearchResult(
                "service",
                service.Id,
                service.Name,
                service.Category,
                service.PartnerId,
                service.Price,
                service.Currency,
                "Reservar",
                $"/buyer/detail/service/{service.Id}",
                coords,
                distanceKm,
                serviceLogo,
                serviceCover,
                serviceImagesByService.TryGetValue(service.Id, out var allUrls) ? allUrls : null));
        }

        return OrderAndTake(items, useGeo, take);
    }

    private async Task<List<SearchResultItem>> SearchProfessionalsInternalAsync(
        bool hasQuery,
        string? filter,
        Guid? countryId,
        Guid? regionId,
        Guid? comunaId,
        bool useGeo,
        double originLat,
        double originLng,
        double maxRadiusKm,
        IReadOnlyDictionary<Guid, Comuna> comunasById,
        int take,
        bool? verified = null)
    {
        var professionalsQuery = _db.Professionals.AsNoTracking()
            .Where(x => x.IsActive && x.IsVerified)
            .Where(x => !countryId.HasValue || x.CountryId == countryId.Value)
            .Where(x => !regionId.HasValue || x.RegionId == regionId.Value)
            .Where(x => !comunaId.HasValue || x.ComunaId == comunaId.Value)
            .Where(x => _db.Partners.Any(p => (p.ComunaId == x.ComunaId || (p.ComunaId == null && p.TenantId == x.TenantId)) && p.IsVisible && p.Type == "C"));

        if (verified.HasValue)
        {
            professionalsQuery = professionalsQuery.Where(x => x.IsVerified == verified.Value);
        }

        if (hasQuery)
        {
            professionalsQuery = professionalsQuery.Where(x =>
                EF.Functions.ILike(x.Name, filter!)
                || (x.Specialty != null && EF.Functions.ILike(x.Specialty, filter!))
                || (x.Bio != null && EF.Functions.ILike(x.Bio, filter!)));
        }

        var fetchLimit = useGeo ? MaxFetchWhenGeo : take;
        var professionals = await professionalsQuery.OrderBy(x => x.Name).Take(fetchLimit).ToListAsync();

        Dictionary<Guid, Partner>? professionalPartnersById = null;
        if (professionals.Count > 0)
        {
            var profPartnerIds = professionals
                .Where(x => x.PartnerId.HasValue)
                .Select(x => x.PartnerId!.Value)
                .Distinct()
                .ToList();
            if (profPartnerIds.Count > 0)
            {
                professionalPartnersById = await _db.Partners.AsNoTracking()
                    .Where(x => profPartnerIds.Contains(x.Id))
                    .ToDictionaryAsync(x => x.Id);
            }
        }

        var items = new List<SearchResultItem>();
        foreach (var professional in professionals)
        {
            var coords = SearchGeoResolver.ResolveProfessionalCoordinates(professional, comunasById);
            double? distanceKm = null;
            if (useGeo)
            {
                distanceKm = SearchGeoResolver.DistanceKmFrom(originLat, originLng, coords);
                if (!SearchGeoResolver.WithinRadius(distanceKm, maxRadiusKm))
                {
                    continue;
                }
            }

            var professionalLogo = professional.ProfilePhotoUrl ?? professional.BannerUrl;
            if (professional.PartnerId is Guid profPartnerId
                && professionalPartnersById is not null
                && professionalPartnersById.TryGetValue(profPartnerId, out var profPartner)
                && !string.IsNullOrWhiteSpace(profPartner.LogoUrl))
            {
                professionalLogo = profPartner.LogoUrl;
            }

            items.Add(BuildSearchResult(
                "professional",
                professional.Id,
                professional.Name,
                professional.Specialty,
                professional.PartnerId,
                null,
                null,
                "Contactar",
                $"/buyer/detail/professional/{professional.Id}",
                coords,
                distanceKm,
                professionalLogo));
        }

        return OrderAndTake(items, useGeo, take);
    }

    private static SearchResultItem BuildSearchResult(
        string type,
        Guid id,
        string name,
        string? category,
        Guid? partnerId,
        decimal? price,
        string? currency,
        string ctaLabel,
        string ctaHref,
        SearchGeoResolver.Coordinates? coords,
        double? distanceKm,
        string? logoUrl = null,
        string? imageUrl = null,
        IReadOnlyList<string>? imageUrls = null)
        => new(
            type,
            id,
            name,
            category,
            partnerId,
            price,
            currency,
            ctaLabel,
            ctaHref,
            distanceKm,
            coords?.Latitude,
            coords?.Longitude,
            logoUrl,
            imageUrl,
            imageUrls);

    private static List<SearchResultItem> OrderAndTake(List<SearchResultItem> items, bool useGeo, int take)
    {
        if (!useGeo)
        {
            return items.Take(take).ToList();
        }

        return items
            .OrderBy(x => x.DistanceKm ?? double.MaxValue)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Take(take)
            .ToList();
    }

    private static string PartnerCtaLabel(string? type)
        => type?.Trim().ToUpperInvariant() switch
        {
            "B" => "Ver agenda",
            "C" => "Ver perfil",
            _ => "Ver vitrina"
        };
}
