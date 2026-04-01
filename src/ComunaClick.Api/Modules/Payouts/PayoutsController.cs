using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ComunaClick.Api.Modules.Payouts.Contracts;
using ComunaClick.Api.Persistence;
using ComunaClick.Api.Persistence.Entities;
using ComunaClick.Common.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ComunaClick.Api.Modules.Payouts;

[ApiController]
[Authorize]
[Route("v1/payouts")]
public sealed class PayoutsController : ControllerBase
{
    private readonly CoreDbContext _db;
    private readonly ITenantContext _tenantContext;

    public PayoutsController(CoreDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    [HttpPost("batches")]
    [Authorize(Policy = "tenant.admin")]
    public async Task<ActionResult<PayoutBatch>> CreateBatch(PayoutBatchCreateRequest request)
    {
        var tenantId = _tenantContext.TenantId;
        if (!tenantId.HasValue)
        {
            return BadRequest(new { message = "TenantId is required." });
        }

        var batch = new PayoutBatch
        {
            TenantId = tenantId.Value,
            PeriodStart = request.PeriodStart,
            PeriodEnd = request.PeriodEnd,
            Status = "open",
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.PayoutBatches.Add(batch);
        await _db.SaveChangesAsync();
        return Created($"/v1/payouts/batches/{batch.Id}", batch);
    }

    [HttpGet("batches/{id:guid}")]
    [Authorize(Policy = "tenant.admin")]
    public async Task<ActionResult<PayoutBatch>> GetBatch(Guid id)
    {
        var batch = await _db.PayoutBatches.AsNoTracking()
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == id);
        return batch is null ? NotFound() : Ok(batch);
    }

    [HttpPost("batches/{id:guid}/close")]
    [Authorize(Policy = "tenant.admin")]
    public async Task<ActionResult<PayoutBatch>> CloseBatch(Guid id)
    {
        var batch = await _db.PayoutBatches.FirstOrDefaultAsync(x => x.Id == id);
        if (batch is null)
        {
            return NotFound();
        }

        batch.Status = "processing";
        await _db.SaveChangesAsync();
        return Ok(batch);
    }

    [HttpPost("batches/{id:guid}/mark-paid")]
    [Authorize(Policy = "tenant.admin")]
    public async Task<ActionResult<PayoutBatch>> MarkPaid(Guid id)
    {
        var batch = await _db.PayoutBatches.FirstOrDefaultAsync(x => x.Id == id);
        if (batch is null)
        {
            return NotFound();
        }

        batch.Status = "paid";
        await _db.SaveChangesAsync();
        return Ok(batch);
    }

    [HttpGet("/v1/partners/{partnerId:guid}/payouts")]
    [Authorize(Policy = "partner.staff")]
    public async Task<ActionResult<IEnumerable<PayoutItem>>> ListByPartner(Guid partnerId)
    {
        var tenantId = _tenantContext.TenantId ?? ResolveGuidClaim(AuthConstants.ClaimTenantId, "tenantId", "tenant_id");
        if (!tenantId.HasValue)
        {
            return BadRequest(new { message = "TenantId is required." });
        }

        var partner = await _db.Partners.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == partnerId && x.TenantId == tenantId.Value);

        if (partner is null)
        {
            return NotFound();
        }

        var scopedPartner = _tenantContext.PartnerId ?? ResolveGuidClaim(AuthConstants.ClaimPartnerId, "partnerId", "partner_id");
        if (scopedPartner.HasValue && scopedPartner.Value != partnerId)
        {
            return Forbid();
        }

        if (!scopedPartner.HasValue)
        {
            var userId = ResolveGuidClaim(JwtRegisteredClaimNames.Sub, ClaimTypes.NameIdentifier);
            if (!userId.HasValue)
            {
                return Forbid();
            }

            var hasAccess = await _db.PartnerStaff.AsNoTracking()
                .AnyAsync(x => x.TenantId == tenantId.Value && x.PartnerId == partnerId && x.UserId == userId.Value);

            if (!hasAccess)
            {
                return Forbid();
            }
        }

        var items = await _db.PayoutItems.AsNoTracking()
            .Where(x => x.PartnerId == partnerId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();
        return Ok(items);
    }

    private Guid? ResolveGuidClaim(params string[] claimTypes)
    {
        foreach (var claimType in claimTypes)
        {
            var value = User.FindFirstValue(claimType);
            if (Guid.TryParse(value, out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }
}
