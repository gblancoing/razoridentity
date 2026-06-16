using ComunaClick.Api.Modules.Catalog;
using ComunaClick.Api.Persistence;
using ComunaClick.Api.Persistence.Entities;
using ComunaClick.Api.Security;
using ComunaClick.Common.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace ComunaClick.Api.Modules.Onboarding;

[ApiController]
[EnableRateLimiting("public-read")]
[Route("v1/public/catalog")]
public sealed class PublicCatalogController : ControllerBase
{
    private readonly CoreDbContext _db;
    private readonly IProductInventoryService _inventoryService;

    public PublicCatalogController(CoreDbContext db, IProductInventoryService inventoryService)
    {
        _db = db;
        _inventoryService = inventoryService;
    }

    [HttpGet("categories")]
    public async Task<ActionResult<IEnumerable<object>>> Categories([FromQuery] string? scope = "commerce")
    {
        var catalogScope = NormalizeCatalogScope(scope);
        var categories = await _db.ProductCategories.AsNoTracking()
            .Where(x => x.IsActive && x.CatalogScope == catalogScope)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync();

        var items = categories
            .GroupBy(x => NormalizeKey(x.Name))
            .Select(group => group
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Name)
                .First())
            .Select(x => new
            {
                x.Id,
                x.Code,
                x.Name,
                x.ImageUrl
            })
            .ToList();

        return Ok(items);
    }

    [HttpGet("subcategories")]
    public async Task<ActionResult<IEnumerable<object>>> Subcategories([FromQuery] Guid categoryId)
    {
        if (categoryId == Guid.Empty)
        {
            return BadRequest(new { message = "categoryId is required." });
        }

        var category = await _db.ProductCategories.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == categoryId && x.IsActive);

        if (category is null)
        {
            return Ok(Array.Empty<object>());
        }

        var familyCategoryIds = await ResolveCategoryFamilyIdsAsync(category.Id, category.Name);

        var items = await _db.ProductSubcategories.AsNoTracking()
            .Where(x => x.IsActive && familyCategoryIds.Contains(x.CategoryId))
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync();

        return Ok(items
            .GroupBy(x => NormalizeKey(x.Name))
            .Select(group => group
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Name)
                .First())
            .Select(x => new
            {
                x.Id,
                x.CategoryId,
                x.Code,
                x.Name
            })
            .ToList());
    }

    [HttpGet("discovery/{categoryCode}")]
    public async Task<ActionResult<object>> Discovery(string categoryCode)
    {
        if (string.IsNullOrWhiteSpace(categoryCode))
        {
            return BadRequest(new { message = "categoryCode is required." });
        }

        var category = await _db.ProductCategories.AsNoTracking()
            .FirstOrDefaultAsync(x => x.IsActive && x.Code.ToLower() == categoryCode.Trim().ToLower());

        if (category is null)
        {
            return NotFound(new { message = "Category not found." });
        }

        var familyCategoryIds = await ResolveCategoryFamilyIdsAsync(category.Id, category.Name);

        var subcategories = await _db.ProductSubcategories.AsNoTracking()
            .Where(x => x.IsActive && familyCategoryIds.Contains(x.CategoryId))
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync();

        var matchTerms = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            category.Name,
            category.Code
        };

        foreach (var subcategory in subcategories)
        {
            if (!string.IsNullOrWhiteSpace(subcategory.Name))
            {
                matchTerms.Add(subcategory.Name);
            }

            if (!string.IsNullOrWhiteSpace(subcategory.Code))
            {
                matchTerms.Add(subcategory.Code);
            }
        }

        var visiblePartners = await _db.Partners.AsNoTracking()
            .Include(x => x.Subcategory)
            .ThenInclude(x => x!.Category)
            .Where(x => x.IsVisible)
            .OrderBy(x => x.Name)
            .ToListAsync();

        var visiblePartnerIds = visiblePartners.Select(x => x.Id).ToHashSet();
        var visibleProfessionalPartnerKeys = visiblePartners
            .Where(x => string.Equals(x.Type, "C", StringComparison.OrdinalIgnoreCase))
            .Select(x => $"{x.TenantId:N}:{x.ComunaId?.ToString("N") ?? string.Empty}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var products = await _db.Products.AsNoTracking()
            .Where(x => x.IsActive && visiblePartnerIds.Contains(x.PartnerId))
            .ToListAsync();

        var services = await _db.Services.AsNoTracking()
            .Where(x => x.IsActive && visiblePartnerIds.Contains(x.PartnerId))
            .ToListAsync();

        var professionals = await _db.Professionals.AsNoTracking()
            .Where(x => x.IsActive && x.IsVerified)
            .ToListAsync();

        var matchingProducts = products
            .Where(x => MatchesAny(x.Category, matchTerms))
            .ToList();

        var matchingServices = services
            .Where(x => MatchesAny(x.Category, matchTerms))
            .ToList();

        var matchingProfessionals = professionals
            .Where(x => visibleProfessionalPartnerKeys.Contains($"{x.TenantId:N}:{x.ComunaId?.ToString("N") ?? string.Empty}"))
            .Where(x => MatchesAny(x.Specialty, matchTerms) || MatchesAny(x.Bio, matchTerms))
            .ToList();

        var businessIds = new HashSet<Guid>();

        foreach (var partner in visiblePartners.Where(x => x.Subcategory?.CategoryId is Guid categoryIdValue && familyCategoryIds.Contains(categoryIdValue)))
        {
            businessIds.Add(partner.Id);
        }

        foreach (var product in matchingProducts)
        {
            businessIds.Add(product.PartnerId);
        }

        var discoveryPartnerIdsForPage = await (
            from link in _db.ProductDiscoverySubcategories.AsNoTracking()
            join sub in _db.ProductSubcategories.AsNoTracking() on link.SubcategoryId equals sub.Id
            join product in _db.Products.AsNoTracking() on link.ProductId equals product.Id
            where sub.IsActive
                  && familyCategoryIds.Contains(sub.CategoryId)
                  && product.IsActive
                  && visiblePartnerIds.Contains(product.PartnerId)
            select product.PartnerId)
            .Distinct()
            .ToListAsync();

        foreach (var partnerId in discoveryPartnerIdsForPage)
        {
            businessIds.Add(partnerId);
        }

        foreach (var service in matchingServices)
        {
            businessIds.Add(service.PartnerId);
        }

        foreach (var partner in visiblePartners.Where(x =>
                     string.Equals(x.Type, "C", StringComparison.OrdinalIgnoreCase)
                     && matchingProfessionals.Any(p => p.TenantId == x.TenantId && p.ComunaId == x.ComunaId)))
        {
            businessIds.Add(partner.Id);
        }

        var businesses = visiblePartners
            .Where(x => businessIds.Contains(x.Id))
            .Select(x => new
            {
                x.Id,
                x.Type,
                x.Name,
                x.Address,
                x.Phone,
                x.Email,
                CategoryName = x.Subcategory?.Category?.Name,
                SubcategoryName = x.Subcategory?.Name
            })
            .ToList();

        var partnerNameById = visiblePartners.ToDictionary(x => x.Id, x => x.Name);

        var serviceItems = matchingServices
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Description,
                x.Category,
                x.Price,
                x.Currency,
                x.PartnerId,
                PartnerName = partnerNameById.TryGetValue(x.PartnerId, out var partnerName) ? partnerName : null
            })
            .ToList();

        var professionalItems = matchingProfessionals
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Specialty,
                x.Bio,
                x.Email,
                x.Phone,
                x.ProfilePhotoUrl
            })
            .ToList();

        return Ok(new
        {
            Category = new
            {
                category.Id,
                category.Code,
                category.Name,
                category.ImageUrl
            },
            Subcategories = subcategories.Select(x => new
            {
                x.Id,
                x.Code,
                x.Name
            }),
            Businesses = businesses,
            Services = serviceItems,
            Professionals = professionalItems
        });
    }

    [HttpGet("discovery/{categoryCode}/nearby")]
    public async Task<ActionResult<object>> DiscoveryNearby(
        string categoryCode,
        [FromQuery] double latitude,
        [FromQuery] double longitude,
        [FromQuery] int limit = 24,
        [FromQuery] string? subcode = null,
        [FromQuery] double maxDistanceKm = 50)
    {
        if (string.IsNullOrWhiteSpace(categoryCode))
        {
            return BadRequest(new { message = "categoryCode is required." });
        }

        if (latitude is < -90 or > 90 || longitude is < -180 or > 180)
        {
            return BadRequest(new { message = "latitude and longitude are required." });
        }

        limit = Math.Clamp(limit, 1, 60);
        maxDistanceKm = Math.Clamp(maxDistanceKm, 1, 200);

        var category = await _db.ProductCategories.AsNoTracking()
            .FirstOrDefaultAsync(x => x.IsActive && x.Code.ToLower() == categoryCode.Trim().ToLower());

        if (category is null)
        {
            return NotFound(new { message = "Category not found." });
        }

        var familyCategoryIds = await ResolveCategoryFamilyIdsAsync(category.Id, category.Name);
        var matchTerms = await ResolveCategoryMatchTermsAsync(category, familyCategoryIds);
        var businessIds = await ResolveBusinessIdsForCategoryAsync(familyCategoryIds, matchTerms);

        ProductSubcategory? subFilter = null;
        if (!string.IsNullOrWhiteSpace(subcode))
        {
            var subTrim = subcode.Trim();
            subFilter = await _db.ProductSubcategories.AsNoTracking()
                .FirstOrDefaultAsync(x => x.IsActive
                    && familyCategoryIds.Contains(x.CategoryId)
                    && x.Code.ToLower() == subTrim.ToLower());
            if (subFilter is null)
            {
                return Ok(new
                {
                    Category = new
                    {
                        category.Id,
                        category.Code,
                        category.Name
                    },
                    UserLocation = new
                    {
                        Latitude = latitude,
                        Longitude = longitude
                    },
                    Businesses = Array.Empty<object>()
                });
            }
        }

        var candidatePartners = await _db.Partners.AsNoTracking()
            .Include(x => x.Subcategory)
            .ThenInclude(x => x!.Category)
            .Where(x => x.IsVisible
                && businessIds.Contains(x.Id)
                && (subFilter == null || x.SubcategoryId == subFilter.Id))
            .ToListAsync();

        var comunaIds = candidatePartners
            .Where(x => x.ComunaId.HasValue)
            .Select(x => x.ComunaId!.Value)
            .Distinct()
            .ToList();

        var comunasById = await _db.Comunas.AsNoTracking()
            .Where(x => x.IsActive && comunaIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id);

        var items = candidatePartners
            .Select(partner =>
            {
                var comuna = partner.ComunaId is Guid comunaId && comunasById.TryGetValue(comunaId, out var c)
                    ? c
                    : null;

                var itemLatitude = partner.Latitude ?? comuna?.Latitude;
                var itemLongitude = partner.Longitude ?? comuna?.Longitude;
                if (itemLatitude is null || itemLongitude is null)
                {
                    return null;
                }

                var distanceKm = CalculateDistanceKm(
                    latitude,
                    longitude,
                    itemLatitude.Value,
                    itemLongitude.Value);

                if (distanceKm > maxDistanceKm)
                {
                    return null;
                }

                return new
                {
                    partner.Id,
                    partner.Type,
                    partner.Name,
                    partner.Address,
                    partner.Phone,
                    partner.Email,
                    partner.LogoUrl,
                    CategoryName = partner.Subcategory?.Category?.Name,
                    SubcategoryName = partner.Subcategory?.Name,
                    ComunaId = comuna?.Id,
                    ComunaName = comuna?.Name,
                    Latitude = itemLatitude.Value,
                    Longitude = itemLongitude.Value,
                    UsesExactLocation = partner.Latitude.HasValue && partner.Longitude.HasValue,
                    DistanceKm = distanceKm
                };
            })
            .Where(x => x is not null)
            .Select(x => x!)
            .OrderBy(x => x.DistanceKm)
            .ThenBy(x => x.Name)
            .Take(limit)
            .ToList();

        return Ok(new
        {
            Category = new
            {
                category.Id,
                category.Code,
                category.Name
            },
            UserLocation = new
            {
                Latitude = latitude,
                Longitude = longitude
            },
            MaxDistanceKm = maxDistanceKm,
            Businesses = items
        });
    }

    [HttpGet("home-feed")]
    public async Task<ActionResult<object>> HomeFeed(
        [FromQuery] double? latitude,
        [FromQuery] double? longitude,
        [FromQuery] int limit = 12,
        [FromQuery] double maxDistanceKm = 50)
    {
        limit = Math.Clamp(limit, 1, 40);
        maxDistanceKm = Math.Clamp(maxDistanceKm, 1, 200);
        var hasLocation = latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180;

        var partners = await _db.Partners.AsNoTracking()
            .Include(x => x.Subcategory)
            .ThenInclude(x => x!.Category)
            .Where(x => x.IsVisible)
            .Where(x =>
                (x.Type == "A" && (
                    _db.Products.Any(p => p.PartnerId == x.Id && p.IsActive)
                    || (x.OffersServices && _db.Services.Any(s => s.PartnerId == x.Id && s.IsActive && s.DeletedAt == null))))
                || (x.Type == "B" && _db.Services.Any(s => s.PartnerId == x.Id && s.IsActive && s.DeletedAt == null))
                || (x.Type == "C" && _db.Professionals.Any(p =>
                    (p.ComunaId == x.ComunaId || (p.ComunaId == null && p.TenantId == x.TenantId))
                    && p.IsActive && p.IsVerified)))
            .ToListAsync();

        var comunaIds = partners.Where(x => x.ComunaId.HasValue).Select(x => x.ComunaId!.Value).Distinct().ToList();
        var comunasById = await _db.Comunas.AsNoTracking()
            .Where(x => x.IsActive && comunaIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id);

        var rankedPartners = partners
            .Select(partner =>
            {
                comunasById.TryGetValue(partner.ComunaId ?? Guid.Empty, out var comuna);
                var itemLatitude = partner.Latitude ?? comuna?.Latitude;
                var itemLongitude = partner.Longitude ?? comuna?.Longitude;
                double? distanceKm = null;
                if (hasLocation && itemLatitude.HasValue && itemLongitude.HasValue)
                {
                    distanceKm = CalculateDistanceKm(
                        latitude!.Value,
                        longitude!.Value,
                        itemLatitude.Value,
                        itemLongitude.Value);
                }

                return new
                {
                    Partner = partner,
                    ComunaName = comuna?.Name,
                    DistanceKm = distanceKm,
                    HasCoordinates = itemLatitude.HasValue && itemLongitude.HasValue
                };
            })
            .Where(x => !hasLocation
                || (x.DistanceKm.HasValue && x.DistanceKm.Value <= maxDistanceKm))
            .OrderBy(x => hasLocation ? x.DistanceKm ?? double.MaxValue : double.MaxValue)
            .ThenByDescending(x => x.Partner.UpdatedAt)
            .Take(limit)
            .ToList();

        var partnerIds = rankedPartners.Select(x => x.Partner.Id).ToHashSet();
        var services = await _db.Services.AsNoTracking()
            .Where(x => x.IsActive && partnerIds.Contains(x.PartnerId))
            .OrderByDescending(x => x.UpdatedAt)
            .ThenBy(x => x.Name)
            .Take(limit)
            .ToListAsync();

        var partnerNameById = rankedPartners.ToDictionary(x => x.Partner.Id, x => x.Partner.Name);
        var partnerDistanceById = rankedPartners.ToDictionary(x => x.Partner.Id, x => x.DistanceKm);

        return Ok(new
        {
            Mode = hasLocation ? "nearby" : "recent",
            MaxDistanceKm = hasLocation ? maxDistanceKm : (double?)null,
            UserLocation = hasLocation
                ? new { Latitude = latitude, Longitude = longitude }
                : null,
            Businesses = rankedPartners.Select(x => new
            {
                x.Partner.Id,
                x.Partner.Type,
                x.Partner.Name,
                x.Partner.Address,
                x.Partner.Phone,
                x.Partner.Email,
                CategoryName = x.Partner.Subcategory?.Category?.Name,
                CategoryCode = x.Partner.Subcategory?.Category?.Code,
                SubcategoryName = x.Partner.Subcategory?.Name,
                x.ComunaName,
                x.DistanceKm,
                CtaHref = $"/buyer/detail/partner/{x.Partner.Id}"
            }),
            Services = services.Select(x => new
            {
                x.Id,
                x.Name,
                x.Description,
                x.Category,
                x.Price,
                x.Currency,
                x.PartnerId,
                PartnerName = partnerNameById.TryGetValue(x.PartnerId, out var partnerName) ? partnerName : null,
                DistanceKm = partnerDistanceById.TryGetValue(x.PartnerId, out var distance) ? distance : null,
                CtaHref = $"/buyer/detail/service/{x.Id}"
            })
        });
    }

    [HttpGet("/v1/public/partners/{id:guid}/profile")]
    public async Task<ActionResult<object>> PartnerProfile(Guid id)
    {
        var partner = await _db.Partners.AsNoTracking()
            .Include(x => x.Subcategory)
            .ThenInclude(x => x!.Category)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (partner is null)
        {
            return NotFound();
        }

        if (!partner.IsVisible
            && !await PartnerAccessAuthorization.CanPreviewUnpublishedPartnerAsync(
                _db,
                User,
                partner.Id,
                HttpContext.RequestAborted))
        {
            return NotFound();
        }

        var isServiceOnlyPartner = string.Equals(partner.Type, "B", StringComparison.OrdinalIgnoreCase);
        var productEntities = isServiceOnlyPartner
            ? new List<Product>()
            : await _db.Products.AsNoTracking()
                .Include(x => x.Inventory)
                .Where(x => x.PartnerId == id && x.IsActive)
                .OrderBy(x => x.Name)
                .ToListAsync();
        if (productEntities.Count > 0)
        {
            await ProductMediaSync.EnrichManyAsync(_db, productEntities, HttpContext.RequestAborted);
            await ProductDiscoverySync.EnrichDiscoveryIdsAsync(_db, productEntities, HttpContext.RequestAborted);
        }

        var productIds = productEntities.Select(x => x.Id).ToList();
        var availableByProduct = productIds.Count == 0
            ? new Dictionary<Guid, int>()
            : await _inventoryService.GetAvailableQuantitiesAsync(productIds, cancellationToken: HttpContext.RequestAborted);
        var products = productEntities.Select(x =>
        {
            var available = availableByProduct.TryGetValue(x.Id, out var qty) ? qty : 0;
            return new
            {
                x.Id,
                x.Name,
                x.Description,
                x.Category,
                x.PartnerCatalogCategoryId,
                CatalogCategoryName = x.CatalogCategoryName,
                Price = (double)x.Price,
                x.Currency,
                x.ImageUrl,
                ImageUrls = x.ImageUrls,
                DiscoverySubcategoryIds = x.DiscoverySubcategoryIds,
                InStock = ProductInventoryRules.IsInStock(available),
                StockQuantity = ProductInventoryRules.GetOnHandQuantity(x.Inventory),
                AvailableQuantity = available
            };
        }).ToList();

        var includeServices = string.Equals(partner.Type, "B", StringComparison.OrdinalIgnoreCase)
                              || (string.Equals(partner.Type, "A", StringComparison.OrdinalIgnoreCase) && partner.OffersServices);

        var serviceRows = !includeServices
            ? new List<ServiceProfileRow>()
            : await _db.Services.AsNoTracking()
                .Where(x => x.PartnerId == id && x.IsActive && x.DeletedAt == null)
                .OrderBy(x => x.Name)
                .Select(x => new ServiceProfileRow(
                    x.Id,
                    x.Name,
                    x.Description,
                    x.Category,
                    (double)x.Price,
                    x.Currency,
                    x.DurationMinutes,
                    x.ImageUrl,
                    x.ServiceAddress,
                    x.IsBookable,
                    x.RequiresOnlinePayment))
                .ToListAsync();

        var serviceIds = serviceRows.Select(x => x.Id).ToList();
        var imageRows = serviceIds.Count == 0
            ? new List<ServiceImageRow>()
            : await _db.ServiceImages.AsNoTracking()
                .Where(x => serviceIds.Contains(x.ServiceId))
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.CreatedAt)
                .Select(x => new ServiceImageRow(x.ServiceId, x.Url))
                .ToListAsync();

        var imagesByService = imageRows
            .GroupBy(x => x.ServiceId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(x => x.Url).ToList());

        var services = serviceRows.Select(s =>
        {
            imagesByService.TryGetValue(s.Id, out var urls);
            var imageUrls = urls ?? Array.Empty<string>();
            var cover = imageUrls.Count > 0 ? imageUrls[0] : s.ImageUrl;
            return new
            {
                s.Id,
                s.Name,
                s.Description,
                s.Category,
                s.Price,
                s.Currency,
                s.DurationMinutes,
                ImageUrl = cover,
                ImageUrls = imageUrls,
                ServiceAddress = s.ServiceAddress,
                s.IsBookable,
                s.RequiresOnlinePayment
            };
        }).ToList();

        var professionals = await _db.Professionals.AsNoTracking()
            .Where(x => x.TenantId == partner.TenantId && x.IsActive && x.IsVerified)
            .Where(x => !partner.ComunaId.HasValue || x.ComunaId == partner.ComunaId)
            .OrderBy(x => x.Name)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Specialty,
                x.Bio,
                x.BannerUrl,
                x.ProfileHeadline,
                x.Email,
                x.Phone,
                x.WebsiteUrl,
                x.InstagramUrl,
                x.FacebookUrl,
                x.LinkedInUrl,
                x.XUrl,
                x.TikTokUrl,
                x.YouTubeUrl,
                x.OtherLinkLabel,
                x.OtherLinkUrl
            })
            .ToListAsync();

        return Ok(new
        {
            Partner = new
            {
                partner.Id,
                partner.Type,
                partner.Name,
                partner.Address,
                partner.Phone,
                partner.Email,
                partner.BannerUrl,
                partner.LogoUrl,
                partner.StorefrontTagline,
                partner.StorefrontAbout,
                partner.StorefrontHighlight1,
                partner.StorefrontHighlight2,
                partner.StorefrontHighlight3,
                partner.WebsiteUrl,
                partner.InstagramUrl,
                partner.FacebookUrl,
                partner.LinkedInUrl,
                partner.XUrl,
                partner.TikTokUrl,
                partner.YouTubeUrl,
                partner.OtherLinkLabel,
                partner.OtherLinkUrl,
                partner.ShippingCourierPaidEnabled,
                partner.ShippingFreeOverAmountEnabled,
                partner.ShippingFreeOverAmount,
                partner.ShippingDeliveryZoneEnabled,
                partner.ShippingFreeEnabled,
                CategoryName = partner.Subcategory?.Category?.Name,
                CategoryCode = partner.Subcategory?.Category?.Code,
                SubcategoryName = partner.Subcategory?.Name,
                OfferLabel = !string.IsNullOrWhiteSpace(partner.StorefrontTagline)
                    ? partner.StorefrontTagline.Trim()
                    : partner.Type switch
                    {
                        "B" => "Servicios activos",
                        "C" => "Profesionales activos",
                        _ => "Productos activos"
                    }
            },
            Products = products,
            Services = services,
            Professionals = professionals
        });
    }

    private static bool MatchesAny(string? value, IEnumerable<string> terms)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<HashSet<string>> ResolveCategoryMatchTermsAsync(ComunaClick.Api.Persistence.Entities.ProductCategory category, IReadOnlyCollection<Guid> familyCategoryIds)
    {
        var subcategories = await _db.ProductSubcategories.AsNoTracking()
            .Where(x => x.IsActive && familyCategoryIds.Contains(x.CategoryId))
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync();

        var matchTerms = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            category.Name,
            category.Code
        };

        foreach (var subcategory in subcategories)
        {
            if (!string.IsNullOrWhiteSpace(subcategory.Name))
            {
                matchTerms.Add(subcategory.Name);
            }

            if (!string.IsNullOrWhiteSpace(subcategory.Code))
            {
                matchTerms.Add(subcategory.Code);
            }
        }

        return matchTerms;
    }

    private async Task<HashSet<Guid>> ResolveBusinessIdsForCategoryAsync(IReadOnlyCollection<Guid> familyCategoryIds, HashSet<string> matchTerms)
    {
        var visiblePartners = await _db.Partners.AsNoTracking()
            .Include(x => x.Subcategory)
            .ThenInclude(x => x!.Category)
            .Where(x => x.IsVisible)
            .OrderBy(x => x.Name)
            .ToListAsync();

        var visiblePartnerIds = visiblePartners.Select(x => x.Id).ToHashSet();
        var visibleProfessionalPartnerKeys = visiblePartners
            .Where(x => string.Equals(x.Type, "C", StringComparison.OrdinalIgnoreCase))
            .Select(x => $"{x.TenantId:N}:{x.ComunaId?.ToString("N") ?? string.Empty}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var products = await _db.Products.AsNoTracking()
            .Where(x => x.IsActive && visiblePartnerIds.Contains(x.PartnerId))
            .ToListAsync();

        var services = await _db.Services.AsNoTracking()
            .Where(x => x.IsActive && visiblePartnerIds.Contains(x.PartnerId))
            .ToListAsync();

        var professionals = await _db.Professionals.AsNoTracking()
            .Where(x => x.IsActive && x.IsVerified)
            .ToListAsync();

        var matchingProducts = products
            .Where(x => MatchesAny(x.Category, matchTerms))
            .ToList();

        var matchingServices = services
            .Where(x => MatchesAny(x.Category, matchTerms))
            .ToList();

        var matchingProfessionals = professionals
            .Where(x => visibleProfessionalPartnerKeys.Contains($"{x.TenantId:N}:{x.ComunaId?.ToString("N") ?? string.Empty}"))
            .Where(x => MatchesAny(x.Specialty, matchTerms) || MatchesAny(x.Bio, matchTerms))
            .ToList();

        var businessIds = new HashSet<Guid>();

        foreach (var partner in visiblePartners.Where(x => x.Subcategory?.CategoryId is Guid categoryIdValue && familyCategoryIds.Contains(categoryIdValue)))
        {
            businessIds.Add(partner.Id);
        }

        foreach (var product in matchingProducts)
        {
            businessIds.Add(product.PartnerId);
        }

        var discoveryPartnerIds = await (
            from link in _db.ProductDiscoverySubcategories.AsNoTracking()
            join sub in _db.ProductSubcategories.AsNoTracking() on link.SubcategoryId equals sub.Id
            join product in _db.Products.AsNoTracking() on link.ProductId equals product.Id
            where sub.IsActive
                  && familyCategoryIds.Contains(sub.CategoryId)
                  && product.IsActive
                  && visiblePartnerIds.Contains(product.PartnerId)
            select product.PartnerId)
            .Distinct()
            .ToListAsync();

        foreach (var partnerId in discoveryPartnerIds)
        {
            businessIds.Add(partnerId);
        }

        foreach (var service in matchingServices)
        {
            businessIds.Add(service.PartnerId);
        }

        foreach (var partner in visiblePartners.Where(x =>
                     string.Equals(x.Type, "C", StringComparison.OrdinalIgnoreCase)
                     && matchingProfessionals.Any(p => p.TenantId == x.TenantId && p.ComunaId == x.ComunaId)))
        {
            businessIds.Add(partner.Id);
        }

        return businessIds;
    }

    private async Task<List<Guid>> ResolveCategoryFamilyIdsAsync(Guid categoryId, string categoryName)
    {
        var normalizedName = NormalizeKey(categoryName);

        return await _db.ProductCategories.AsNoTracking()
            .Where(x => x.IsActive && (x.Id == categoryId || (x.Name != null && x.Name.Trim().ToLower() == normalizedName)))
            .Select(x => x.Id)
            .Distinct()
            .ToListAsync();
    }

    private static double CalculateDistanceKm(double latitude1, double longitude1, double latitude2, double longitude2)
    {
        const double earthRadiusKm = 6371d;

        var dLatitude = DegreesToRadians(latitude2 - latitude1);
        var dLongitude = DegreesToRadians(longitude2 - longitude1);
        var lat1 = DegreesToRadians(latitude1);
        var lat2 = DegreesToRadians(latitude2);

        var a = Math.Sin(dLatitude / 2) * Math.Sin(dLatitude / 2)
            + Math.Cos(lat1) * Math.Cos(lat2)
            * Math.Sin(dLongitude / 2) * Math.Sin(dLongitude / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return Math.Round(earthRadiusKm * c, 1, MidpointRounding.AwayFromZero);
    }

    private static double DegreesToRadians(double degrees) => degrees * (Math.PI / 180d);

    private static string NormalizeKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        // Normalize for dedup across minor variations (diacritics, NBSP, extra whitespace).
        var normalized = value.Trim().ToLowerInvariant()
            .Replace('\u00A0', ' ')
            .Replace("á", "a", StringComparison.Ordinal)
            .Replace("é", "e", StringComparison.Ordinal)
            .Replace("í", "i", StringComparison.Ordinal)
            .Replace("ó", "o", StringComparison.Ordinal)
            .Replace("ú", "u", StringComparison.Ordinal)
            .Replace("ñ", "n", StringComparison.Ordinal);

        while (normalized.Contains("  ", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("  ", " ", StringComparison.Ordinal);
        }

        return normalized;
    }

    private static string NormalizeCatalogScope(string? scope)
        => string.Equals(scope, "service", StringComparison.OrdinalIgnoreCase) ? "service" : "commerce";

    private sealed record ServiceImageRow(Guid ServiceId, string Url);

    private sealed record ServiceProfileRow(
        Guid Id,
        string Name,
        string? Description,
        string? Category,
        double Price,
        string Currency,
        int DurationMinutes,
        string? ImageUrl,
        string? ServiceAddress,
        bool IsBookable,
        bool RequiresOnlinePayment);
}
