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
        var items = await _db.ProductCategories.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Select(x => new
            {
                x.Id,
                x.Code,
                x.Name
            })
            .ToListAsync();

        return Ok(items);
    }

    [HttpGet("subcategories")]
    public async Task<ActionResult<IEnumerable<object>>> Subcategories([FromQuery] Guid categoryId)
    {
        if (categoryId == Guid.Empty)
        {
            return BadRequest(new { message = "categoryId is required." });
        }

        var items = await _db.ProductSubcategories.AsNoTracking()
            .Where(x => x.IsActive && x.CategoryId == categoryId)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Select(x => new
            {
                x.Id,
                x.CategoryId,
                x.Code,
                x.Name
            })
            .ToListAsync();

        return Ok(items);
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

        var subcategories = await _db.ProductSubcategories.AsNoTracking()
            .Where(x => x.IsActive && x.CategoryId == category.Id)
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

        foreach (var partner in visiblePartners.Where(x => x.Subcategory?.CategoryId == category.Id))
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

    private static bool MatchesAny(string? value, IEnumerable<string> terms)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));
    }
}
