using ComunaClick.Api.Modules.Catalog.Contracts;
using ComunaClick.Api.Persistence;
using ComunaClick.Api.Persistence.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
        => Ok(await _db.Professionals.AsNoTracking().ToListAsync());

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Professional>> Get(Guid id)
    {
        var professional = await _db.Professionals.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return professional is null ? NotFound() : Ok(professional);
    }

    [HttpPost]
    public async Task<ActionResult<Professional>> Create(ProfessionalCreateRequest request)
    {
        var tenantId = _tenantContext.TenantId;
        if (!tenantId.HasValue)
        {
            return BadRequest(new { message = "TenantId is required." });
        }

        var professional = new Professional
        {
            TenantId = tenantId.Value,
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
}
