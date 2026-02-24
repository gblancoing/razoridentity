using ComunaClick.Api.Modules.Search.Contracts;
using ComunaClick.Api.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ComunaClick.Api.Modules.Search;

[ApiController]
[AllowAnonymous]
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
        [FromQuery] int? limit)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Ok(Array.Empty<SearchResultItem>());
        }

        var take = Math.Clamp(limit ?? 20, 1, 100);
        var filter = $"%{query.Trim()}%";
        var results = new List<SearchResultItem>();

        if (string.IsNullOrWhiteSpace(type) || type.Equals("partners", StringComparison.OrdinalIgnoreCase))
        {
            var partners = await _db.Partners.AsNoTracking()
                .Where(x => x.IsVisible && EF.Functions.ILike(x.Name, filter))
                .OrderBy(x => x.Name)
                .Take(take)
                .Select(x => new SearchResultItem("partner", x.Id, x.Name, null, x.Id, null, null))
                .ToListAsync();
            results.AddRange(partners);
        }

        if (string.IsNullOrWhiteSpace(type) || type.Equals("products", StringComparison.OrdinalIgnoreCase))
        {
            var products = await _db.Products.AsNoTracking()
                .Where(x => x.IsActive && EF.Functions.ILike(x.Name, filter))
                .OrderBy(x => x.Name)
                .Take(take)
                .Select(x => new SearchResultItem("product", x.Id, x.Name, x.Category, x.PartnerId, x.Price, x.Currency))
                .ToListAsync();
            results.AddRange(products);
        }

        if (string.IsNullOrWhiteSpace(type) || type.Equals("services", StringComparison.OrdinalIgnoreCase))
        {
            var services = await _db.Services.AsNoTracking()
                .Where(x => x.IsActive && EF.Functions.ILike(x.Name, filter))
                .OrderBy(x => x.Name)
                .Take(take)
                .Select(x => new SearchResultItem("service", x.Id, x.Name, x.Category, x.PartnerId, x.Price, x.Currency))
                .ToListAsync();
            results.AddRange(services);
        }

        if (string.IsNullOrWhiteSpace(type) || type.Equals("professionals", StringComparison.OrdinalIgnoreCase))
        {
            var professionals = await _db.Professionals.AsNoTracking()
                .Where(x => x.IsActive && EF.Functions.ILike(x.Name, filter))
                .OrderBy(x => x.Name)
                .Take(take)
                .Select(x => new SearchResultItem("professional", x.Id, x.Name, x.Specialty, null, null, null))
                .ToListAsync();
            results.AddRange(professionals);
        }

        return Ok(results.Take(take));
    }

    [HttpGet("partners")]
    public async Task<ActionResult<IEnumerable<SearchResultItem>>> SearchPartners([FromQuery] string? query, [FromQuery] int? limit)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Ok(Array.Empty<SearchResultItem>());
        }

        var take = Math.Clamp(limit ?? 20, 1, 100);
        var filter = $"%{query.Trim()}%";

        var partners = await _db.Partners.AsNoTracking()
            .Where(x => x.IsVisible && EF.Functions.ILike(x.Name, filter))
            .OrderBy(x => x.Name)
            .Take(take)
            .Select(x => new SearchResultItem("partner", x.Id, x.Name, null, x.Id, null, null))
            .ToListAsync();

        return Ok(partners);
    }

    [HttpGet("professionals")]
    public async Task<ActionResult<IEnumerable<SearchResultItem>>> SearchProfessionals([FromQuery] string? query, [FromQuery] bool? verified, [FromQuery] int? limit)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Ok(Array.Empty<SearchResultItem>());
        }

        var take = Math.Clamp(limit ?? 20, 1, 100);
        var filter = $"%{query.Trim()}%";

        var baseQuery = _db.Professionals.AsNoTracking()
            .Where(x => x.IsActive && EF.Functions.ILike(x.Name, filter));

        if (verified.HasValue)
        {
            baseQuery = baseQuery.Where(x => x.IsVerified == verified.Value);
        }

        var professionals = await baseQuery
            .OrderBy(x => x.Name)
            .Take(take)
            .Select(x => new SearchResultItem("professional", x.Id, x.Name, x.Specialty, null, null, null))
            .ToListAsync();

        return Ok(professionals);
    }
}
