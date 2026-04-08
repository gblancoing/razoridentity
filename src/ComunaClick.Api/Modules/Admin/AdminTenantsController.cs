using ComunaClick.Api.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ComunaClick.Api.Modules.Admin;

[ApiController]
[Route("v1/admin/tenants")]
[Authorize(Policy = "platform.admin")]
public sealed class AdminTenantsController : ControllerBase
{
    private readonly CoreDbContext _db;

    public AdminTenantsController(CoreDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<AdminTenantListItemDto>>> List(CancellationToken cancellationToken)
    {
        var items = await _db.Tenants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(x => x.Comuna)
            .GroupJoin(
                _db.Partners.IgnoreQueryFilters(),
                tenant => tenant.Id,
                partner => partner.TenantId,
                (tenant, partners) => new AdminTenantListItemDto(
                    tenant.Id,
                    tenant.Name,
                    tenant.Timezone,
                    tenant.Comuna != null ? tenant.Comuna.Name : null,
                    tenant.IsActive,
                    partners.Count(),
                    tenant.UpdatedAt))
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<AdminTenantListItemDto>> Update(Guid id, AdminTenantUpdateRequest request, CancellationToken cancellationToken)
    {
        var entity = await _db.Tenants
            .IgnoreQueryFilters()
            .Include(x => x.Comuna)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
        {
            return NotFound();
        }

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            entity.Name = request.Name.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.Timezone))
        {
            entity.Timezone = request.Timezone.Trim();
        }

        if (request.IsActive.HasValue)
        {
            entity.IsActive = request.IsActive.Value;
        }

        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        var partnerCount = await _db.Partners.IgnoreQueryFilters().CountAsync(x => x.TenantId == entity.Id, cancellationToken);

        return Ok(new AdminTenantListItemDto(
            entity.Id,
            entity.Name,
            entity.Timezone,
            entity.Comuna?.Name,
            entity.IsActive,
            partnerCount,
            entity.UpdatedAt));
    }
}
