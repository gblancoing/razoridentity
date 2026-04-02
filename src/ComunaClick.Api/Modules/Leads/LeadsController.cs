using ComunaClick.Api.Modules.Leads.Contracts;
using ComunaClick.Api.Persistence;
using ComunaClick.Api.Persistence.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace ComunaClick.Api.Modules.Leads;

[ApiController]
[Authorize(Policy = "partner.staff")]
[Route("v1/leads")]
public sealed class LeadsController : ControllerBase
{
    private readonly CoreDbContext _db;
    private readonly ITenantContext _tenantContext;

    public LeadsController(CoreDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Lead>> Get(Guid id)
    {
        var lead = await _db.Leads.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return lead is null ? NotFound() : Ok(lead);
    }

    [HttpGet("/v1/professionals/{professionalId:guid}/leads")]
    public async Task<ActionResult<IEnumerable<Lead>>> ListByProfessional(Guid professionalId)
    {
        var leads = await _db.Leads.AsNoTracking()
            .Where(x => x.ProfessionalId == professionalId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();
        return Ok(leads);
    }

    [HttpGet("/v1/partners/{partnerId:guid}/leads")]
    public async Task<ActionResult<IEnumerable<Lead>>> ListByPartner(Guid partnerId)
    {
        var tenantId = _tenantContext.TenantId;
        if (!tenantId.HasValue)
        {
            return BadRequest(new { message = "TenantId is required." });
        }

        var scopedPartner = _tenantContext.PartnerId;
        if (scopedPartner.HasValue && scopedPartner.Value != partnerId)
        {
            return Forbid();
        }

        var partner = await _db.Partners.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == partnerId && x.TenantId == tenantId.Value);

        if (partner is null)
        {
            return NotFound();
        }

        var professionalIds = await _db.Professionals.AsNoTracking()
            .Where(x => x.TenantId == partner.TenantId)
            .Where(x => !partner.ComunaId.HasValue || x.ComunaId == partner.ComunaId)
            .Select(x => x.Id)
            .ToListAsync();

        if (professionalIds.Count == 0)
        {
            return Ok(Array.Empty<Lead>());
        }

        var leads = await _db.Leads.AsNoTracking()
            .Where(x => x.TenantId == tenantId.Value && professionalIds.Contains(x.ProfessionalId))
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        return Ok(leads);
    }

    [HttpPost]
    [AllowAnonymous]
    [EnableRateLimiting("public-write")]
    public async Task<ActionResult<Lead>> Create(LeadCreateRequest request)
    {
        var tenantId = _tenantContext.TenantId;
        if (!tenantId.HasValue)
        {
            return BadRequest(new { message = "TenantId is required." });
        }

        var lead = new Lead
        {
            TenantId = tenantId.Value,
            ProfessionalId = request.ProfessionalId,
            CustomerId = request.CustomerId,
            Status = "new",
            Message = request.Message,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _db.Leads.Add(lead);
        await _db.SaveChangesAsync();
        return Created($"/v1/leads/{lead.Id}", lead);
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<Lead>> UpdateStatus(Guid id, LeadStatusUpdateRequest request)
    {
        var lead = await _db.Leads.FirstOrDefaultAsync(x => x.Id == id);
        if (lead is null)
        {
            return NotFound();
        }

        lead.Status = request.Status.Trim();
        lead.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(lead);
    }
}
