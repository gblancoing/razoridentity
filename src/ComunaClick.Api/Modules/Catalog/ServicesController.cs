using ComunaClick.Api.Geo;
using ComunaClick.Api.Modules.Catalog.Contracts;
using ComunaClick.Api.Persistence;
using ComunaClick.Api.Persistence.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ComunaClick.Api.Modules.Catalog;

[ApiController]
[Authorize(Policy = "partner.staff")]
[Route("v1/services")]
public sealed class ServicesController : ControllerBase
{
    private readonly CoreDbContext _db;
    private readonly ITenantContext _tenantContext;

    public ServicesController(CoreDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Service>> Get(Guid id)
    {
        var service = await _db.Services.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return service is null ? NotFound() : Ok(service);
    }

    [HttpGet("/v1/partners/{partnerId:guid}/services")]
    public async Task<ActionResult<IEnumerable<Service>>> ListByPartner(Guid partnerId)
    {
        var scopedPartner = _tenantContext.PartnerId;
        if (scopedPartner.HasValue && scopedPartner.Value != partnerId)
        {
            return Forbid();
        }

        var services = await _db.Services.AsNoTracking()
            .Where(x => x.PartnerId == partnerId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();
        return Ok(services);
    }

    [HttpPost]
    public async Task<ActionResult<Service>> Create(ServiceCreateRequest request)
    {
        var tenantId = _tenantContext.TenantId;
        if (!tenantId.HasValue)
        {
            return BadRequest(new { message = "TenantId is required." });
        }

        var partnerId = _tenantContext.PartnerId ?? request.PartnerId;
        if (partnerId == Guid.Empty)
        {
            return BadRequest(new { message = "PartnerId is required." });
        }

        if (_tenantContext.PartnerId.HasValue && request.PartnerId != Guid.Empty && request.PartnerId != _tenantContext.PartnerId.Value)
        {
            return Forbid();
        }

        var geo = await GeoContextResolver.ResolveFromTenantAsync(_db, tenantId.Value);
        var service = new Service
        {
            TenantId = tenantId.Value,
            PartnerId = partnerId,
            CountryId = geo.CountryId,
            RegionId = geo.RegionId,
            ComunaId = geo.ComunaId,
            Name = request.Name.Trim(),
            Description = request.Description,
            Category = request.Category,
            Price = request.Price,
            Currency = string.IsNullOrWhiteSpace(request.Currency) ? "CLP" : request.Currency.Trim(),
            DurationMinutes = request.DurationMinutes ?? 30,
            IsActive = request.IsActive ?? true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _db.Services.Add(service);
        await _db.SaveChangesAsync();
        return Created($"/v1/services/{service.Id}", service);
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<Service>> Update(Guid id, ServiceUpdateRequest request)
    {
        var service = await _db.Services.FirstOrDefaultAsync(x => x.Id == id);
        if (service is null)
        {
            return NotFound();
        }

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            service.Name = request.Name.Trim();
        }

        if (request.Description is not null)
        {
            service.Description = request.Description;
        }

        if (request.Category is not null)
        {
            service.Category = request.Category;
        }

        if (request.Price.HasValue)
        {
            service.Price = request.Price.Value;
        }

        if (!string.IsNullOrWhiteSpace(request.Currency))
        {
            service.Currency = request.Currency.Trim();
        }

        if (request.DurationMinutes.HasValue)
        {
            service.DurationMinutes = request.DurationMinutes.Value;
        }

        if (request.IsActive.HasValue)
        {
            service.IsActive = request.IsActive.Value;
        }

        service.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(service);
    }
}
