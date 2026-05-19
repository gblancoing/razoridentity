using ComunaClick.Api.Persistence;
using ComunaClick.Api.Persistence.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace ComunaClick.Api.Modules.Onboarding;

[ApiController]
[AllowAnonymous]
[EnableRateLimiting("public-read")]
[Route("v1/public/geo")]
public sealed class PublicGeoController : ControllerBase
{
    private readonly CoreDbContext _db;

    public PublicGeoController(CoreDbContext db)
    {
        _db = db;
    }

    [HttpGet("countries")]
    public async Task<ActionResult<IEnumerable<object>>> GetCountries()
    {
        var countries = await _db.Countries.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new { x.Id, x.Code, x.Name })
            .ToListAsync();

        return Ok(countries);
    }

    [HttpGet("regions")]
    public async Task<ActionResult<IEnumerable<object>>> GetRegions([FromQuery] Guid countryId)
    {
        var regions = await _db.Regions.AsNoTracking()
            .Where(x => x.CountryId == countryId && x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new { x.Id, x.CountryId, x.Code, x.Name })
            .ToListAsync();

        return Ok(regions);
    }

    [HttpGet("comunas")]
    public async Task<ActionResult<IEnumerable<object>>> GetComunas([FromQuery] Guid regionId)
    {
        var comunas = await _db.Comunas.AsNoTracking()
            .Where(x => x.RegionId == regionId && x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new { x.Id, x.RegionId, x.Code, x.Name })
            .ToListAsync();

        return Ok(comunas);
    }

    [HttpGet("tenant-by-comuna/{comunaId:guid}")]
    public async Task<ActionResult<object>> ResolveTenantByComuna(Guid comunaId)
    {
        var comuna = await _db.Comunas.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == comunaId && x.IsActive);
        if (comuna is null)
        {
            return NotFound(new { message = "Comuna not found." });
        }

        var region = await _db.Regions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == comuna.RegionId && x.IsActive);
        if (region is null)
        {
            return NotFound(new { message = "Region not found for comuna." });
        }

        var country = await _db.Countries.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == region.CountryId && x.IsActive);
        if (country is null)
        {
            return NotFound(new { message = "Country not found for comuna." });
        }

        var tenant = await _db.Tenants.IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ComunaId == comunaId && x.IsActive);
        if (tenant is null)
        {
            return NotFound(new { message = "Tenant not found for comuna." });
        }

        return Ok(new
        {
            TenantId = tenant.Id,
            ComunaId = comuna.Id,
            TenantName = tenant.Name,
            ComunaName = comuna.Name,
            RegionName = region.Name,
            CountryName = country.Name
        });
    }

    [HttpPost("tenant-by-comuna/{comunaId:guid}")]
    [EnableRateLimiting("public-write")]
    public async Task<ActionResult<object>> EnsureTenantByComuna(Guid comunaId)
    {
        var comuna = await _db.Comunas.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == comunaId && x.IsActive);
        if (comuna is null)
        {
            return NotFound(new { message = "Comuna not found." });
        }

        var region = await _db.Regions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == comuna.RegionId && x.IsActive);
        if (region is null)
        {
            return NotFound(new { message = "Region not found for comuna." });
        }

        var country = await _db.Countries.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == region.CountryId && x.IsActive);
        if (country is null)
        {
            return NotFound(new { message = "Country not found for comuna." });
        }

        var tenantsQuery = _db.Tenants.IgnoreQueryFilters();
        var tenant = await tenantsQuery.FirstOrDefaultAsync(x => x.ComunaId == comunaId && x.IsActive);
        if (tenant is null)
        {
            tenant = await tenantsQuery.FirstOrDefaultAsync(x => x.Name == comuna.Name && x.IsActive);
            if (tenant is not null && !tenant.ComunaId.HasValue)
            {
                tenant.ComunaId = comunaId;
                tenant.UpdatedAt = DateTimeOffset.UtcNow;
                try
                {
                    await _db.SaveChangesAsync();
                }
                catch (DbUpdateException)
                {
                    _db.Entry(tenant).State = EntityState.Detached;
                    tenant = await tenantsQuery.FirstOrDefaultAsync(x => x.ComunaId == comunaId && x.IsActive)
                        ?? await tenantsQuery.FirstOrDefaultAsync(x => x.Name == comuna.Name && x.IsActive);
                }
            }

            if (tenant is null)
            {
                tenant = new Tenant
                {
                    ComunaId = comunaId,
                    Name = await BuildUniqueTenantNameAsync(comuna.Name, comuna.Code),
                    Timezone = "America/Santiago",
                    ConfigJson = "{}",
                    IsActive = true,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                };

                _db.Tenants.Add(tenant);
                try
                {
                    await _db.SaveChangesAsync();
                }
                catch (DbUpdateException)
                {
                    _db.Entry(tenant).State = EntityState.Detached;
                    tenant = await tenantsQuery.FirstOrDefaultAsync(x => x.ComunaId == comunaId && x.IsActive)
                        ?? await tenantsQuery.FirstOrDefaultAsync(x => x.Name == comuna.Name && x.IsActive);
                    if (tenant is null)
                    {
                        throw;
                    }
                }
            }
        }

        return Ok(new
        {
            TenantId = tenant.Id,
            ComunaId = comuna.Id,
            TenantName = tenant.Name,
            ComunaName = comuna.Name,
            RegionName = region.Name,
            CountryName = country.Name
        });
    }

    private async Task<string> BuildUniqueTenantNameAsync(string comunaName, string comunaCode)
    {
        var baseName = string.IsNullOrWhiteSpace(comunaName) ? "ComunaClic" : comunaName.Trim();
        var codeSuffix = string.IsNullOrWhiteSpace(comunaCode) ? "COMUNA" : comunaCode.Trim().ToUpperInvariant();

        var tenantsQuery = _db.Tenants.IgnoreQueryFilters().AsNoTracking();
        if (!await tenantsQuery.AnyAsync(x => x.Name == baseName))
        {
            return baseName;
        }

        var codeCandidate = $"{baseName} ({codeSuffix})";
        if (!await tenantsQuery.AnyAsync(x => x.Name == codeCandidate))
        {
            return codeCandidate;
        }

        var attempt = 2;
        while (attempt <= 1000)
        {
            var candidate = $"{baseName} ({codeSuffix}-{attempt})";
            if (!await tenantsQuery.AnyAsync(x => x.Name == candidate))
            {
                return candidate;
            }

            attempt++;
        }

        return $"{baseName} ({Guid.NewGuid():N})";
    }
}
