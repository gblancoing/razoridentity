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

    public PublicCatalogController(CoreDbContext db)
    {
        _db = db;
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
                x.Phone
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
        [FromQuery] string? subcode = null)
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

        var nearbyBusinesses = await _db.Partners.AsNoTracking()
            .Include(x => x.Subcategory)
            .ThenInclude(x => x!.Category)
            .Join(
                _db.Comunas.AsNoTracking().Where(x => x.IsActive && x.Latitude != null && x.Longitude != null),
                partner => partner.ComunaId,
                comuna => comuna.Id,
                (partner, comuna) => new
                {
                    Partner = partner,
                    Comuna = comuna
                })
            .Where(x => x.Partner.IsVisible
                && businessIds.Contains(x.Partner.Id)
                && (subFilter == null || x.Partner.SubcategoryId == subFilter.Id))
            .ToListAsync();

        var items = nearbyBusinesses
            .Select(x => new
            {
                x.Partner.Id,
                x.Partner.Type,
                x.Partner.Name,
                x.Partner.Address,
                x.Partner.Phone,
                x.Partner.Email,
                CategoryName = x.Partner.Subcategory != null ? x.Partner.Subcategory.Category != null ? x.Partner.Subcategory.Category.Name : null : null,
                SubcategoryName = x.Partner.Subcategory != null ? x.Partner.Subcategory.Name : null,
                ComunaId = x.Comuna.Id,
                ComunaName = x.Comuna.Name,
                Latitude = x.Partner.Latitude ?? x.Comuna.Latitude!.Value,
                Longitude = x.Partner.Longitude ?? x.Comuna.Longitude!.Value,
                UsesExactLocation = x.Partner.Latitude.HasValue && x.Partner.Longitude.HasValue,
                DistanceKm = CalculateDistanceKm(
                    latitude,
                    longitude,
                    x.Partner.Latitude ?? x.Comuna.Latitude.Value,
                    x.Partner.Longitude ?? x.Comuna.Longitude.Value)
            })
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
            Businesses = items
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

        var products = await _db.Products.AsNoTracking()
            .Where(x => x.PartnerId == id && x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Description,
                x.Category,
                x.Price,
                x.Currency
            })
            .ToListAsync();

        var services = await _db.Services.AsNoTracking()
            .Where(x => x.PartnerId == id && x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Description,
                x.Category,
                x.Price,
                x.Currency,
                x.DurationMinutes
            })
            .ToListAsync();

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
                x.Email,
                x.Phone
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
                CategoryName = partner.Subcategory?.Category?.Name,
                CategoryCode = partner.Subcategory?.Category?.Code,
                SubcategoryName = partner.Subcategory?.Name,
                OfferLabel = partner.Type switch
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
        => value?.Trim().ToLowerInvariant() ?? string.Empty;

    private static string NormalizeCatalogScope(string? scope)
        => string.Equals(scope, "service", StringComparison.OrdinalIgnoreCase) ? "service" : "commerce";
}
