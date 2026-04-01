using ComunaClick.Api.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ComunaClick.Api.Modules.Onboarding;

[ApiController]
[Route("v1/public/catalog")]
public sealed class PublicCatalogController : ControllerBase
{
    private readonly CoreDbContext _db;

    public PublicCatalogController(CoreDbContext db)
    {
        _db = db;
    }

    [HttpGet("categories")]
    public async Task<ActionResult<IEnumerable<object>>> Categories()
    {
        var categories = await _db.ProductCategories.AsNoTracking()
            .Where(x => x.IsActive)
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
                x.Name
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
                category.Name
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

    [HttpGet("/v1/public/partners/{id:guid}/profile")]
    public async Task<ActionResult<object>> PartnerProfile(Guid id)
    {
        var partner = await _db.Partners.AsNoTracking()
            .Include(x => x.Subcategory)
            .ThenInclude(x => x!.Category)
            .FirstOrDefaultAsync(x => x.Id == id && x.IsVisible);

        if (partner is null)
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

    private async Task<List<Guid>> ResolveCategoryFamilyIdsAsync(Guid categoryId, string categoryName)
    {
        var normalizedName = NormalizeKey(categoryName);

        return await _db.ProductCategories.AsNoTracking()
            .Where(x => x.IsActive && (x.Id == categoryId || (x.Name != null && x.Name.Trim().ToLower() == normalizedName)))
            .Select(x => x.Id)
            .Distinct()
            .ToListAsync();
    }

    private static string NormalizeKey(string? value)
        => value?.Trim().ToLowerInvariant() ?? string.Empty;
}
