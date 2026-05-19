using ComunaClick.Api.Modules.Leads.Contracts;
using ComunaClick.Api.Persistence;
using ComunaClick.Api.Persistence.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace ComunaClick.Api.Modules.Leads;

[ApiController]
[Route("v1/leads")]
public sealed class LeadsController : ControllerBase
{
    private static readonly HashSet<string> AllowedWorkflowStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "new",
        "pending",
        "contacted",
        "in_follow_up",
        "won",
        "lost",
        "closed"
    };

    private static readonly HashSet<string> AllowedPriorities = new(StringComparer.OrdinalIgnoreCase)
    {
        "high",
        "normal",
        "low"
    };

    private readonly CoreDbContext _db;
    private readonly ITenantContext _tenantContext;

    public LeadsController(CoreDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    [Authorize(Policy = "partner.staff")]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Lead>> Get(Guid id)
    {
        var tenantId = _tenantContext.TenantId;
        if (!tenantId.HasValue)
        {
            return BadRequest(new { message = "TenantId is required." });
        }

        var lead = await _db.Leads.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId.Value);
        if (lead is null)
        {
            return NotFound();
        }

        if (!await CanAccessProfessionalAsync(lead.ProfessionalId, tenantId.Value))
        {
            return Forbid();
        }

        return Ok(lead);
    }

    [Authorize(Policy = "partner.staff")]
    [HttpGet("/v1/professionals/{professionalId:guid}/leads")]
    public async Task<ActionResult<IEnumerable<Lead>>> ListByProfessional(Guid professionalId)
    {
        var tenantId = _tenantContext.TenantId;
        if (!tenantId.HasValue)
        {
            return BadRequest(new { message = "TenantId is required." });
        }

        if (!await CanAccessProfessionalAsync(professionalId, tenantId.Value))
        {
            return Forbid();
        }

        var leads = await _db.Leads.AsNoTracking()
            .Where(x => x.TenantId == tenantId.Value && x.ProfessionalId == professionalId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();
        return Ok(leads);
    }

    [Authorize(Policy = "partner.staff")]
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
    [Authorize(Policy = "buyer.customer")]
    [EnableRateLimiting("public-write")]
    public async Task<ActionResult<Lead>> Create(LeadCreateRequest request)
    {
        var tenantId = _tenantContext.TenantId;
        if (!tenantId.HasValue)
        {
            return BadRequest(new { message = "TenantId is required." });
        }

        if (request.CustomerId == Guid.Empty)
        {
            return BadRequest(new { message = "CustomerId is required." });
        }

        var hasCustomer = await _db.Customers.AsNoTracking()
            .AnyAsync(x => x.Id == request.CustomerId && x.TenantId == tenantId.Value);

        if (!hasCustomer)
        {
            return BadRequest(new { message = "CustomerId does not exist for current tenant." });
        }

        var hasProfessional = await _db.Professionals.AsNoTracking()
            .AnyAsync(x =>
                x.Id == request.ProfessionalId &&
                x.TenantId == tenantId.Value &&
                x.IsActive &&
                x.IsVerified);

        if (!hasProfessional)
        {
            return BadRequest(new { message = "Professional is not available for contact." });
        }

        var lead = new Lead
        {
            TenantId = tenantId.Value,
            ProfessionalId = request.ProfessionalId,
            CustomerId = request.CustomerId,
            Status = "new",
            Priority = "normal",
            Message = request.Message,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _db.Leads.Add(lead);
        await _db.SaveChangesAsync();
        return Created($"/v1/leads/{lead.Id}", lead);
    }

    [Authorize(Policy = "partner.staff")]
    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<Lead>> UpdateStatus(Guid id, LeadStatusUpdateRequest request)
    {
        var tenantId = _tenantContext.TenantId;
        if (!tenantId.HasValue)
        {
            return BadRequest(new { message = "TenantId is required." });
        }

        var lead = await _db.Leads.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId.Value);
        if (lead is null)
        {
            return NotFound();
        }

        if (!await CanAccessProfessionalAsync(lead.ProfessionalId, tenantId.Value))
        {
            return Forbid();
        }

        if (!TryNormalizeStatus(request.Status, out var normalizedStatus, out var statusValidationError))
        {
            return BadRequest(new { message = statusValidationError });
        }

        if (normalizedStatus is "won" or "lost")
        {
            return BadRequest(new { message = "Use workflow endpoint to close lead as won/lost with outcome reason." });
        }

        lead.Status = normalizedStatus;
        lead.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(lead);
    }

    [Authorize(Policy = "partner.staff")]
    [HttpPatch("{id:guid}/workflow")]
    public async Task<ActionResult<Lead>> UpdateWorkflow(Guid id, LeadWorkflowUpdateRequest request)
    {
        var tenantId = _tenantContext.TenantId;
        if (!tenantId.HasValue)
        {
            return BadRequest(new { message = "TenantId is required." });
        }

        var lead = await _db.Leads.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId.Value);
        if (lead is null)
        {
            return NotFound();
        }

        if (!await CanAccessProfessionalAsync(lead.ProfessionalId, tenantId.Value))
        {
            return Forbid();
        }

        if (request.Status is not null)
        {
            if (!TryNormalizeStatus(request.Status, out var normalizedStatus, out var statusValidationError))
            {
                return BadRequest(new { message = statusValidationError });
            }

            lead.Status = normalizedStatus;
        }

        if (request.Priority is not null)
        {
            if (string.IsNullOrWhiteSpace(request.Priority))
            {
                lead.Priority = null;
            }
            else
            {
                var priority = request.Priority.Trim().ToLowerInvariant();
                if (!AllowedPriorities.Contains(priority))
                {
                    return BadRequest(new { message = "Priority must be one of: high, normal, low." });
                }

                lead.Priority = priority;
            }
        }

        if (request.Owner is not null)
        {
            lead.Owner = string.IsNullOrWhiteSpace(request.Owner) ? null : request.Owner.Trim()[..Math.Min(120, request.Owner.Trim().Length)];
        }

        if (request.NextFollowUpAt.HasValue)
        {
            lead.NextFollowUpAt = request.NextFollowUpAt.Value;
        }

        if (request.InternalNote is not null)
        {
            lead.InternalNote = string.IsNullOrWhiteSpace(request.InternalNote) ? null : request.InternalNote.Trim();
        }

        if (request.OutcomeReason is not null)
        {
            var reason = string.IsNullOrWhiteSpace(request.OutcomeReason) ? null : request.OutcomeReason.Trim();
            if (reason is { Length: > 240 })
            {
                reason = reason[..240];
            }

            lead.OutcomeReason = reason;
        }

        if (lead.Status.Equals("lost", StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(lead.OutcomeReason) &&
            (request.Status is not null || request.OutcomeReason is not null))
        {
            return BadRequest(new { message = "OutcomeReason is required when lead status is lost." });
        }

        lead.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(lead);
    }

    private static bool TryNormalizeStatus(string? rawStatus, out string normalizedStatus, out string validationError)
    {
        normalizedStatus = string.Empty;
        validationError = string.Empty;

        if (string.IsNullOrWhiteSpace(rawStatus))
        {
            validationError = "Status is required.";
            return false;
        }

        normalizedStatus = rawStatus.Trim().ToLowerInvariant();
        if (normalizedStatus == "follow_up")
        {
            normalizedStatus = "in_follow_up";
        }
        else if (normalizedStatus == "closed")
        {
            // Keep backward compatibility with old "closed" values by mapping to "won".
            normalizedStatus = "won";
        }

        if (!AllowedWorkflowStatuses.Contains(normalizedStatus))
        {
            validationError = "Status is not valid.";
            return false;
        }

        return true;
    }

    private async Task<bool> CanAccessProfessionalAsync(Guid professionalId, Guid tenantId)
    {
        var scopedPartner = _tenantContext.PartnerId;
        if (!scopedPartner.HasValue)
        {
            return await _db.Professionals.AsNoTracking()
                .AnyAsync(x => x.Id == professionalId && x.TenantId == tenantId);
        }

        var partner = await _db.Partners.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == scopedPartner.Value && x.TenantId == tenantId);

        if (partner is null)
        {
            return false;
        }

        return await _db.Professionals.AsNoTracking()
            .AnyAsync(x =>
                x.Id == professionalId &&
                x.TenantId == tenantId &&
                (x.ComunaId == partner.ComunaId || (!x.ComunaId.HasValue && !partner.ComunaId.HasValue)));
    }
}
