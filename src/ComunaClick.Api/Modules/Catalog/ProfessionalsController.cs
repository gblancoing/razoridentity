using ComunaClick.Api.Geo;
using ComunaClick.Api.Modules.Catalog.Contracts;
using ComunaClick.Api.Modules.Onboarding;
using ComunaClick.Api.Modules.Onboarding.Contracts;
using ComunaClick.Api.Persistence;
using ComunaClick.Api.Persistence.Entities;
using ComunaClick.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace ComunaClick.Api.Modules.Catalog;

[ApiController]
[Authorize(Policy = "partner.staff")]
[Route("v1/professionals")]
public sealed class ProfessionalsController : ControllerBase
{
    private readonly CoreDbContext _db;
    private readonly ITenantContext _tenantContext;

    public ProfessionalsController(CoreDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Professional>>> List()
    {
        var tenantId = _tenantContext.TenantId;
        var query = _db.Professionals.AsNoTracking().AsQueryable();
        if (tenantId.HasValue)
        {
            query = query.Where(x => x.TenantId == tenantId.Value);
        }

        return Ok(await query.OrderByDescending(x => x.CreatedAt).ToListAsync());
    }

    [HttpGet("/v1/partners/{partnerId:guid}/professionals")]
    public async Task<ActionResult<IEnumerable<Professional>>> ListByPartner(Guid partnerId)
    {
        var access = await PartnerAccessAuthorization.EnsurePartnerAccessAsync(
            _db,
            User,
            partnerId,
            _tenantContext.TenantId,
            _tenantContext.PartnerId,
            HttpContext.RequestAborted);

        if (access == PartnerAccessResult.NotFound)
        {
            return NotFound();
        }

        if (access == PartnerAccessResult.Forbidden)
        {
            return Forbid();
        }

        var partner = await _db.Partners.AsNoTracking().FirstOrDefaultAsync(x => x.Id == partnerId);
        if (partner is null)
        {
            return NotFound();
        }

        var partnerEmail = partner.Email?.Trim().ToLowerInvariant();
        var query = _db.Professionals.AsNoTracking()
            .Where(x => x.TenantId == partner.TenantId)
            .Where(x =>
                x.PartnerId == partnerId
                || (x.PartnerId == null && partnerEmail != null && x.Email != null && x.Email.ToLower() == partnerEmail)
                || (x.PartnerId == null && partnerEmail == null && x.Name == partner.Name));

        var professionals = await query
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();
        return Ok(professionals);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Professional>> Get(Guid id)
    {
        var professional = await _db.Professionals.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return professional is null ? NotFound() : Ok(professional);
    }

    [AllowAnonymous]
    [EnableRateLimiting("public-read")]
    [HttpGet("/v1/public/professionals/{id:guid}")]
    public async Task<ActionResult<Professional>> GetPublic(Guid id)
    {
        var professional = await _db.Professionals.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.IsActive && x.IsVerified);

        if (professional is null)
        {
            return NotFound();
        }

        var hasVisiblePartner = await _db.Partners.AsNoTracking()
            .AnyAsync(x =>
                x.IsVisible &&
                x.Type == "C" &&
                x.TenantId == professional.TenantId &&
                (x.ComunaId == professional.ComunaId || (!x.ComunaId.HasValue && !professional.ComunaId.HasValue)));

        if (!hasVisiblePartner)
        {
            return NotFound();
        }

        return Ok(professional);
    }

    [HttpPost]
    public async Task<ActionResult<Professional>> Create(ProfessionalCreateRequest request)
    {
        var tenantId = _tenantContext.TenantId;
        if (!tenantId.HasValue)
        {
            return BadRequest(new { message = "TenantId is required." });
        }

        var geo = await GeoContextResolver.ResolveFromTenantAsync(_db, tenantId.Value);
        var partnerId = _tenantContext.PartnerId ?? request.PartnerId;
        var professional = new Professional
        {
            TenantId = tenantId.Value,
            PartnerId = partnerId == Guid.Empty ? null : partnerId,
            CountryId = geo.CountryId,
            RegionId = geo.RegionId,
            ComunaId = geo.ComunaId,
            Name = request.Name.Trim(),
            Email = request.Email,
            Phone = request.Phone,
            Specialty = request.Specialty,
            Bio = request.Bio,
            IsVerified = request.IsVerified ?? false,
            IsActive = request.IsActive ?? true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.Professionals.Add(professional);
        await _db.SaveChangesAsync();
        return Created($"/v1/professionals/{professional.Id}", professional);
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<Professional>> Update(Guid id, ProfessionalUpdateRequest request)
    {
        var professional = await _db.Professionals.FirstOrDefaultAsync(x => x.Id == id);
        if (professional is null)
        {
            return NotFound();
        }

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            professional.Name = request.Name.Trim();
        }

        if (request.Email is not null)
        {
            professional.Email = request.Email;
        }

        if (request.Phone is not null)
        {
            professional.Phone = request.Phone;
        }

        if (request.Specialty is not null)
        {
            professional.Specialty = request.Specialty;
        }

        if (request.Bio is not null)
        {
            professional.Bio = request.Bio;
        }

        if (request.IsVerified.HasValue)
        {
            professional.IsVerified = request.IsVerified.Value;
        }

        if (request.IsActive.HasValue)
        {
            professional.IsActive = request.IsActive.Value;
        }

        await _db.SaveChangesAsync();
        return Ok(professional);
    }

    [HttpPatch("{id:guid}/web-links")]
    public async Task<ActionResult<Professional>> UpdateWebLinks(Guid id, ProfileWebLinksUpdateRequest request)
    {
        var professional = await _db.Professionals.FirstOrDefaultAsync(x => x.Id == id);
        if (professional is null)
        {
            return NotFound();
        }

        if (professional.PartnerId.HasValue)
        {
            var access = await PartnerAccessAuthorization.EnsurePartnerAccessAsync(
                _db,
                User,
                professional.PartnerId.Value,
                _tenantContext.TenantId,
                _tenantContext.PartnerId,
                HttpContext.RequestAborted);

            if (access == PartnerAccessResult.Forbidden)
            {
                return Forbid();
            }
        }
        else if (_tenantContext.TenantId != professional.TenantId)
        {
            return Forbid();
        }

        var validationError = ProfileWebLinksNormalizer.Validate(request);
        if (validationError is not null)
        {
            return BadRequest(new { message = validationError });
        }

        ProfileWebLinksNormalizer.ApplyToProfessional(professional, request);
        await _db.SaveChangesAsync();
        return Ok(professional);
    }
}
