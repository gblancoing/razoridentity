using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
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
        var tenantId = _tenantContext.TenantId;
        if (!tenantId.HasValue)
        {
            return BadRequest(new { message = "TenantId is required." });
        }

        var batch = await _db.PayoutBatches.AsNoTracking()
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId.Value);
        return batch is null ? NotFound() : Ok(batch);
    }

    [HttpPost("batches/{id:guid}/close")]
    [Authorize(Policy = "tenant.admin")]
    public async Task<ActionResult<PayoutBatch>> CloseBatch(Guid id)
    {
        var tenantId = _tenantContext.TenantId;
        if (!tenantId.HasValue)
        {
            return BadRequest(new { message = "TenantId is required." });
        }

        var batch = await _db.PayoutBatches.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId.Value);
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
        var tenantId = _tenantContext.TenantId;
        if (!tenantId.HasValue)
        {
            return BadRequest(new { message = "TenantId is required." });
        }

        var batch = await _db.PayoutBatches.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId.Value);
        if (batch is null)
        {
            return NotFound();
        }

        batch.Status = "paid";
        await _db.SaveChangesAsync();
        return Ok(batch);
    }

    [HttpGet("/v1/admin/payouts/batches")]
    [Authorize(Policy = "platform.admin")]
    public async Task<ActionResult<IReadOnlyList<Admin.AdminPayoutBatchListItemDto>>> ListBatchesForAdmin(
        [FromQuery] string? status,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to)
    {
        var query = _db.PayoutBatches.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalized = status.Trim().ToLowerInvariant();
            query = query.Where(x => x.Status.ToLower() == normalized);
        }

        if (from.HasValue)
        {
            query = query.Where(x => x.CreatedAt >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(x => x.CreatedAt <= to.Value);
        }

        var batches = await query
            .OrderByDescending(x => x.CreatedAt)
            .Take(500)
            .Select(x => new
            {
                x.Id,
                x.TenantId,
                x.PeriodStart,
                x.PeriodEnd,
                x.Status,
                x.CreatedAt,
                ItemCount = x.Items.Count,
                NetTotal = x.Items.Sum(i => (decimal?)i.NetAmount) ?? 0m
            })
            .ToListAsync();

        var tenantIds = batches.Select(x => x.TenantId).Distinct().ToList();
        var tenantNames = await _db.Tenants.AsNoTracking()
            .Where(x => tenantIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Name })
            .ToDictionaryAsync(x => x.Id, x => x.Name);

        var items = batches
            .Select(x => new Admin.AdminPayoutBatchListItemDto(
                x.Id,
                x.TenantId,
                tenantNames.TryGetValue(x.TenantId, out var tenantName) ? tenantName : "(sin tenant)",
                x.PeriodStart,
                x.PeriodEnd,
                x.Status,
                x.ItemCount,
                x.NetTotal,
                x.CreatedAt))
            .ToList();

        return Ok(items);
    }

    [HttpGet("/v1/partners/{partnerId:guid}/payouts")]
    [Authorize(Policy = "partner.staff")]
    public async Task<ActionResult<IEnumerable<PartnerPayoutItemResponse>>> ListByPartner(
        Guid partnerId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] string? batchStatus)
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

        var query = _db.PayoutItems.AsNoTracking()
            .Join(_db.PayoutBatches.AsNoTracking(),
                item => item.BatchId,
                batch => batch.Id,
                (item, batch) => new PartnerPayoutItemResponse(
                    item.Id,
                    item.BatchId,
                    item.PartnerId,
                    item.GrossAmount,
                    item.CommissionAmount,
                    item.SubscriptionDeduction,
                    item.NetAmount,
                    item.Currency,
                    item.CreatedAt,
                    batch.Status))
            .Where(x => x.PartnerId == partnerId)
            .AsQueryable();

        if (from.HasValue)
        {
            query = query.Where(x => x.CreatedAt >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(x => x.CreatedAt <= to.Value);
        }

        if (!string.IsNullOrWhiteSpace(batchStatus))
        {
            var normalizedStatus = batchStatus.Trim().ToLowerInvariant();
            query = query.Where(x => x.BatchStatus != null && x.BatchStatus.ToLower() == normalizedStatus);
        }

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        return Ok(items);
    }

    [HttpGet("/v1/partners/{partnerId:guid}/payouts/export.csv")]
    [Authorize(Policy = "partner.staff")]
    public async Task<IActionResult> ExportByPartnerCsv(
        Guid partnerId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] string? batchStatus)
    {
        var result = await ListByPartner(partnerId, from, to, batchStatus);
        if (result.Result is not null)
        {
            return result.Result;
        }

        var rows = result.Value?.ToList() ?? [];
        var csv = new StringBuilder();
        csv.AppendLine("payout_id,batch_id,batch_status,created_at,gross,commission,subscription,net,currency");
        foreach (var row in rows)
        {
            csv.AppendLine(string.Join(",",
                row.Id,
                row.BatchId,
                EscapeCsv(row.BatchStatus),
                row.CreatedAt.UtcDateTime.ToString("O"),
                row.GrossAmount,
                row.CommissionAmount,
                row.SubscriptionDeduction,
                row.NetAmount,
                EscapeCsv(row.Currency)));
        }

        var fileName = $"payouts_partner_{partnerId}_{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.csv";
        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv; charset=utf-8", fileName);
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

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\n'))
        {
            return value;
        }

        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
