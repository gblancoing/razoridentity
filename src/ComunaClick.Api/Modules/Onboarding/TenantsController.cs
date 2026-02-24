using ComunaClick.Api.Modules.Onboarding.Contracts.Tenants;
using ComunaClick.Api.Persistence;
using ComunaClick.Api.Persistence.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ComunaClick.Api.Modules.Onboarding;

[ApiController]
[Authorize(Policy = "platform.admin")]
[Route("v1/tenants")]
public sealed class TenantsController : ControllerBase
{
    private readonly CoreDbContext _db;

    public TenantsController(CoreDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Tenant>>> List()
        => Ok(await _db.Tenants.AsNoTracking().ToListAsync());

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Tenant>> Get(Guid id)
    {
        var tenant = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return tenant is null ? NotFound() : Ok(tenant);
    }

    [HttpPost]
    public async Task<ActionResult<Tenant>> Create(TenantCreateRequest request)
    {
        var exists = await _db.Tenants.AnyAsync(x => x.Name == request.Name);
        if (exists)
        {
            return Conflict(new { message = "Tenant name already exists." });
        }

        var tenant = new Tenant
        {
            Name = request.Name.Trim(),
            Timezone = string.IsNullOrWhiteSpace(request.Timezone) ? "America/Santiago" : request.Timezone.Trim(),
            ConfigJson = string.IsNullOrWhiteSpace(request.ConfigJson) ? "{}" : request.ConfigJson,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _db.Tenants.Add(tenant);
        await _db.SaveChangesAsync();
        return Created($"/v1/tenants/{tenant.Id}", tenant);
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<Tenant>> Update(Guid id, TenantUpdateRequest request)
    {
        var tenant = await _db.Tenants.FirstOrDefaultAsync(x => x.Id == id);
        if (tenant is null)
        {
            return NotFound();
        }

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            var exists = await _db.Tenants.AnyAsync(x => x.Name == request.Name && x.Id != id);
            if (exists)
            {
                return Conflict(new { message = "Tenant name already exists." });
            }
            tenant.Name = request.Name.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.Timezone))
        {
            tenant.Timezone = request.Timezone.Trim();
        }

        if (request.ConfigJson is not null)
        {
            tenant.ConfigJson = request.ConfigJson;
        }

        tenant.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(tenant);
    }
}
