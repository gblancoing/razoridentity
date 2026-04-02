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
        [FromQuery] int? limit)
    {
        var take = Math.Clamp(limit ?? 20, 1, 100);
        var hasQuery = !string.IsNullOrWhiteSpace(query);
        var filter = hasQuery ? $"%{query!.Trim()}%" : null;
        var results = new List<SearchResultItem>();

        if (string.IsNullOrWhiteSpace(type) || type.Equals("partners", StringComparison.OrdinalIgnoreCase))
        {
            var partnersQuery = _db.Partners.AsNoTracking()
                .Where(x => x.IsVisible)
                .Where(x => !countryId.HasValue || x.CountryId == countryId.Value)
                .Where(x => !regionId.HasValue || x.RegionId == regionId.Value)
                .Where(x => !comunaId.HasValue || x.ComunaId == comunaId.Value)
                .Where(x => x.Type != "A" || _db.Products.Any(p => p.PartnerId == x.Id && p.IsActive))
                .Where(x => x.Type != "B" || _db.Services.Any(s => s.PartnerId == x.Id && s.IsActive))
                .Where(x => x.Type != "C" || _db.Professionals.Any(p => (p.ComunaId == x.ComunaId || (p.ComunaId == null && p.TenantId == x.TenantId)) && p.IsActive && p.IsVerified));

            if (hasQuery)
            {
                partnersQuery = partnersQuery.Where(x => EF.Functions.ILike(x.Name, filter!));
            }

            var partners = await partnersQuery
                .OrderBy(x => x.Name)
                .Take(take)
                .Select(x => new SearchResultItem("partner", x.Id, x.Name, x.Type, x.Id, null, null, PartnerCtaLabel(x.Type), $"/buyer/detail/partner/{x.Id}"))
                .ToListAsync();
            results.AddRange(partners);
        }

        if (string.IsNullOrWhiteSpace(type) || type.Equals("products", StringComparison.OrdinalIgnoreCase))
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

            var products = await productsQuery
                .OrderBy(x => x.Name)
                .Take(take)
                .Select(x => new SearchResultItem("product", x.Id, x.Name, x.Category, x.PartnerId, x.Price, x.Currency, "Comprar", $"/buyer/detail/product/{x.Id}"))
                .ToListAsync();
            results.AddRange(products);
        }

        if (string.IsNullOrWhiteSpace(type) || type.Equals("services", StringComparison.OrdinalIgnoreCase))
        {
            var servicesQuery = _db.Services.AsNoTracking()
                .Where(x => x.IsActive)
                .Where(x => !countryId.HasValue || x.CountryId == countryId.Value)
                .Where(x => !regionId.HasValue || x.RegionId == regionId.Value)
                .Where(x => !comunaId.HasValue || x.ComunaId == comunaId.Value)
                .Where(x => _db.Partners.Any(p => p.Id == x.PartnerId && p.IsVisible));

            if (hasQuery)
            {
                servicesQuery = servicesQuery.Where(x => EF.Functions.ILike(x.Name, filter!));
            }

            var services = await servicesQuery
                .OrderBy(x => x.Name)
                .Take(take)
                .Select(x => new SearchResultItem("service", x.Id, x.Name, x.Category, x.PartnerId, x.Price, x.Currency, "Reservar", $"/buyer/detail/service/{x.Id}"))
                .ToListAsync();
            results.AddRange(services);
        }

        if (string.IsNullOrWhiteSpace(type) || type.Equals("professionals", StringComparison.OrdinalIgnoreCase))
        {
            var professionalsQuery = _db.Professionals.AsNoTracking()
                .Where(x => x.IsActive && x.IsVerified)
                .Where(x => !countryId.HasValue || x.CountryId == countryId.Value)
                .Where(x => !regionId.HasValue || x.RegionId == regionId.Value)
                .Where(x => !comunaId.HasValue || x.ComunaId == comunaId.Value)
                .Where(x => _db.Partners.Any(p => (p.ComunaId == x.ComunaId || (p.ComunaId == null && p.TenantId == x.TenantId)) && p.IsVisible && p.Type == "C"));

            if (hasQuery)
            {
                professionalsQuery = professionalsQuery.Where(x => EF.Functions.ILike(x.Name, filter!));
            }

            var professionals = await professionalsQuery
                .OrderBy(x => x.Name)
                .Take(take)
                .Select(x => new SearchResultItem("professional", x.Id, x.Name, x.Specialty, null, null, null, "Contactar", $"/buyer/detail/professional/{x.Id}"))
                .ToListAsync();
            results.AddRange(professionals);
        }

        return Ok(results.Take(take));
    }

    [HttpGet("partners")]
    public async Task<ActionResult<IEnumerable<SearchResultItem>>> SearchPartners([FromQuery] string? query, [FromQuery] Guid? countryId, [FromQuery] Guid? regionId, [FromQuery] Guid? comunaId, [FromQuery] int? limit)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Ok(Array.Empty<SearchResultItem>());
        }

        var take = Math.Clamp(limit ?? 20, 1, 100);
        var filter = $"%{query.Trim()}%";

        var partners = await _db.Partners.AsNoTracking()
            .Where(x => x.IsVisible)
            .Where(x => !countryId.HasValue || x.CountryId == countryId.Value)
            .Where(x => !regionId.HasValue || x.RegionId == regionId.Value)
            .Where(x => !comunaId.HasValue || x.ComunaId == comunaId.Value)
            .Where(x => x.Type != "A" || _db.Products.Any(p => p.PartnerId == x.Id && p.IsActive))
            .Where(x => x.Type != "B" || _db.Services.Any(s => s.PartnerId == x.Id && s.IsActive))
            .Where(x => x.Type != "C" || _db.Professionals.Any(p => (p.ComunaId == x.ComunaId || (p.ComunaId == null && p.TenantId == x.TenantId)) && p.IsActive && p.IsVerified))
            .Where(x => EF.Functions.ILike(x.Name, filter))
            .OrderBy(x => x.Name)
            .Take(take)
            .Select(x => new SearchResultItem("partner", x.Id, x.Name, x.Type, x.Id, null, null, PartnerCtaLabel(x.Type), $"/buyer/detail/partner/{x.Id}"))
            .ToListAsync();

        return Ok(partners);
    }

    [HttpGet("professionals")]
    public async Task<ActionResult<IEnumerable<SearchResultItem>>> SearchProfessionals([FromQuery] string? query, [FromQuery] Guid? countryId, [FromQuery] Guid? regionId, [FromQuery] Guid? comunaId, [FromQuery] bool? verified, [FromQuery] int? limit)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Ok(Array.Empty<SearchResultItem>());
        }

        var take = Math.Clamp(limit ?? 20, 1, 100);
        var filter = $"%{query.Trim()}%";

        var baseQuery = _db.Professionals.AsNoTracking()
            .Where(x => x.IsActive && x.IsVerified)
            .Where(x => !countryId.HasValue || x.CountryId == countryId.Value)
            .Where(x => !regionId.HasValue || x.RegionId == regionId.Value)
            .Where(x => !comunaId.HasValue || x.ComunaId == comunaId.Value)
            .Where(x => _db.Partners.Any(p => (p.ComunaId == x.ComunaId || (p.ComunaId == null && p.TenantId == x.TenantId)) && p.IsVisible && p.Type == "C"))
            .Where(x => EF.Functions.ILike(x.Name, filter));

        if (verified.HasValue)
        {
            baseQuery = baseQuery.Where(x => x.IsVerified == verified.Value);
        }

        var professionals = await baseQuery
            .OrderBy(x => x.Name)
            .Take(take)
            .Select(x => new SearchResultItem("professional", x.Id, x.Name, x.Specialty, null, null, null, "Contactar", $"/buyer/detail/professional/{x.Id}"))
            .ToListAsync();

        return Ok(professionals);
    }

    private static string PartnerCtaLabel(string? type)
        => type?.Trim().ToUpperInvariant() switch
        {
            "B" => "Ver agenda",
            "C" => "Ver perfil",
            _ => "Ver vitrina"
        };
}
