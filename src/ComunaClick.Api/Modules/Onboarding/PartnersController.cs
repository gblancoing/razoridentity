using ComunaClick.Api.Modules.Onboarding.Contracts.Partners;
using ComunaClick.Api.Persistence;
using ComunaClick.Api.Persistence.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ComunaClick.Api.Modules.Onboarding;

[ApiController]
[Authorize(Policy = "tenant.admin")]
[Route("v1/partners")]
public sealed class PartnersController : ControllerBase
{
    private readonly CoreDbContext _db;
    private readonly ITenantContext _tenantContext;

    public PartnersController(CoreDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Partner>>> List()
    {
        return Ok(await _db.Partners.AsNoTracking().ToListAsync());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Partner>> Get(Guid id)
    {
        var partner = await _db.Partners.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return partner is null ? NotFound() : Ok(partner);
    }

    [HttpPost]
    public async Task<ActionResult<Partner>> Create(PartnerCreateRequest request)
    {
        var tenantId = _tenantContext.TenantId;
        if (!tenantId.HasValue)
        {
            return BadRequest(new { message = "TenantId is required." });
        }

        var exists = await _db.Partners.AnyAsync(x => x.TenantId == tenantId.Value && x.Name == request.Name);
        if (exists)
        {
            return Conflict(new { message = "Partner name already exists for this tenant." });
        }

        var partner = new Partner
        {
            TenantId = tenantId.Value,
            Type = request.Type.Trim(),
            Name = request.Name.Trim(),
            Rut = request.Rut,
            Address = request.Address,
            Phone = request.Phone,
            Email = request.Email,
            IsVisible = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _db.Partners.Add(partner);
        await _db.SaveChangesAsync();
        return Created($"/v1/partners/{partner.Id}", partner);
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<Partner>> Update(Guid id, PartnerUpdateRequest request)
    {
        var partner = await _db.Partners.FirstOrDefaultAsync(x => x.Id == id);
        if (partner is null)
        {
            return NotFound();
        }

        var scopedPartner = _tenantContext.PartnerId;
        if (scopedPartner.HasValue && scopedPartner.Value != partner.Id)
        {
            return Forbid();
        }

        if (!string.IsNullOrWhiteSpace(request.Type))
        {
            partner.Type = request.Type.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            var exists = await _db.Partners.AnyAsync(x =>
                x.TenantId == partner.TenantId &&
                x.Name == request.Name &&
                x.Id != id);
            if (exists)
            {
                return Conflict(new { message = "Partner name already exists for this tenant." });
            }
            partner.Name = request.Name.Trim();
        }

        if (request.Rut is not null)
        {
            partner.Rut = request.Rut;
        }

        if (request.Address is not null)
        {
            partner.Address = request.Address;
        }

        if (request.Phone is not null)
        {
            partner.Phone = request.Phone;
        }

        if (request.Email is not null)
        {
            partner.Email = request.Email;
        }

        if (request.IsVisible.HasValue)
        {
            partner.IsVisible = request.IsVisible.Value;
        }

        partner.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(partner);
    }

    [HttpPatch("{id:guid}/visibility")]
    public async Task<ActionResult<Partner>> UpdateVisibility(Guid id, PartnerVisibilityRequest request)
    {
        var partner = await _db.Partners.FirstOrDefaultAsync(x => x.Id == id);
        if (partner is null)
        {
            return NotFound();
        }

        var scopedPartner = _tenantContext.PartnerId;
        if (scopedPartner.HasValue && scopedPartner.Value != partner.Id)
        {
            return Forbid();
        }

        partner.IsVisible = request.IsVisible;
        partner.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(partner);
    }

    [HttpPost("{id:guid}/staff")]
    public async Task<ActionResult<PartnerStaff>> AddStaff(Guid id, PartnerStaffCreateRequest request)
    {
        var tenantId = _tenantContext.TenantId;
        if (!tenantId.HasValue)
        {
            return BadRequest(new { message = "TenantId is required." });
        }

        var exists = await _db.PartnerStaff.AnyAsync(x => x.PartnerId == id && x.UserId == request.UserId);
        if (exists)
        {
            return Conflict(new { message = "User already linked to this partner." });
        }

        var staff = new PartnerStaff
        {
            TenantId = tenantId.Value,
            PartnerId = id,
            UserId = request.UserId,
            Role = string.IsNullOrWhiteSpace(request.Role) ? "staff" : request.Role.Trim(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.PartnerStaff.Add(staff);
        await _db.SaveChangesAsync();
        return Created($"/v1/partners/{id}/staff/{staff.Id}", staff);
    }
}
