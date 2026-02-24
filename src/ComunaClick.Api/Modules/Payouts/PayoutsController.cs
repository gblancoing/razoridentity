using ComunaClick.Api.Modules.Payouts.Contracts;
using ComunaClick.Api.Persistence;
using ComunaClick.Api.Persistence.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ComunaClick.Api.Modules.Payouts;

[ApiController]
[Authorize(Policy = "tenant.admin")]
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
    public async Task<ActionResult<PayoutBatch>> GetBatch(Guid id)
    {
        var batch = await _db.PayoutBatches.AsNoTracking()
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == id);
        return batch is null ? NotFound() : Ok(batch);
    }

    [HttpPost("batches/{id:guid}/close")]
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
    public async Task<ActionResult<IEnumerable<PayoutItem>>> ListByPartner(Guid partnerId)
    {
        var scopedPartner = _tenantContext.PartnerId;
        if (scopedPartner.HasValue && scopedPartner.Value != partnerId)
        {
            return Forbid();
        }

        var items = await _db.PayoutItems.AsNoTracking()
            .Where(x => x.PartnerId == partnerId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();
        return Ok(items);
    }
}
