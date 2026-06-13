using ComunaClick.Api.Modules.Marketplace;
using ComunaClick.Api.Persistence;
using ComunaClick.Api.Persistence.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ComunaClick.Api.Modules.Admin;

[ApiController]
[Route("v1/admin/marketplace")]
[Authorize(Policy = "platform.admin")]
public sealed class AdminMarketplaceController : ControllerBase
{
    private readonly CoreDbContext _db;
    private readonly MercadoPagoWebhookService _webhookService;

    public AdminMarketplaceController(CoreDbContext db, MercadoPagoWebhookService webhookService)
    {
        _db = db;
        _webhookService = webhookService;
    }

    [HttpGet("/v1/admin/sellers")]
    public async Task<ActionResult<IReadOnlyList<AdminSellerListItemDto>>> ListSellers(CancellationToken cancellationToken)
    {
        var sellers = await _db.Sellers
            .AsNoTracking()
            .Include(x => x.MercadoPagoAccount)
            .Include(x => x.FeeConfiguration)
            .OrderBy(x => x.Name)
            .Take(500)
            .ToListAsync(cancellationToken);

        if (sellers.Count == 0)
        {
            return Ok(Array.Empty<AdminSellerListItemDto>());
        }

        var sellerIds = sellers.Select(x => x.Id).ToList();
        var paymentCounts = await _db.Payments
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.SellerId != null && sellerIds.Contains(x.SellerId.Value))
            .GroupBy(x => x.SellerId!.Value)
            .Select(g => new { SellerId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SellerId, x => x.Count, cancellationToken);

        var items = sellers
            .Select(x => new AdminSellerListItemDto(
                x.Id,
                x.TenantId,
                x.Name,
                x.Email,
                x.IsActive,
                x.MercadoPagoAccount?.ConnectionStatus ?? "disconnected",
                x.MercadoPagoAccount?.MpUserId,
                x.MercadoPagoAccount?.ConnectedAt,
                x.FeeConfiguration?.PercentageFee ?? 0m,
                x.FeeConfiguration?.FixedFeeAmount ?? 0m,
                paymentCounts.TryGetValue(x.Id, out var count) ? count : 0,
                x.UpdatedAt))
            .ToList();

        return Ok(items);
    }

    [HttpPost("payments/{id:guid}/sync")]
    public async Task<IActionResult> RetryPaymentSync(Guid id, CancellationToken cancellationToken)
    {
        var exists = await _db.Payments
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(x => x.Id == id, cancellationToken);

        if (!exists)
        {
            return NotFound();
        }

        await _webhookService.SyncPaymentAsync(id, cancellationToken);
        return Accepted();
    }

    [HttpGet("fees")]
    public async Task<ActionResult<IReadOnlyList<AdminSellerFeeItemDto>>> ListFees(CancellationToken cancellationToken)
    {
        var fees = await _db.SellerFeeConfigurations
            .AsNoTracking()
            .Join(_db.Partners.IgnoreQueryFilters().AsNoTracking(),
                f => f.SellerId,
                p => p.Id,
                (f, p) => new AdminSellerFeeItemDto(f.SellerId, p.Name, f.FixedFeeAmount, f.PercentageFee, f.IsActive))
            .OrderBy(x => x.SellerName)
            .ToListAsync(cancellationToken);

        return Ok(fees);
    }

    [HttpPut("fees")]
    public async Task<IActionResult> UpdateGlobalFee([FromBody] AdminGlobalFeeUpdateRequest request, CancellationToken cancellationToken)
    {
        if (request.PercentageFee < 0 || request.FixedFeeAmount < 0)
        {
            return BadRequest(new { message = "Las comisiones no pueden ser negativas." });
        }

        var updated = await _db.SellerFeeConfigurations
            .Where(x => x.IsActive)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.PercentageFee, request.PercentageFee)
                .SetProperty(x => x.FixedFeeAmount, request.FixedFeeAmount)
                .SetProperty(x => x.UpdatedAt, DateTimeOffset.UtcNow),
            cancellationToken);

        return Ok(new { updated, percentageFee = request.PercentageFee, fixedFeeAmount = request.FixedFeeAmount });
    }

    [HttpPut("fees/{sellerId:guid}")]
    public async Task<IActionResult> UpdateSellerFee(Guid sellerId, [FromBody] AdminSellerFeeUpdateRequest request, CancellationToken cancellationToken)
    {
        if (request.PercentageFee < 0 || request.FixedFeeAmount < 0)
        {
            return BadRequest(new { message = "Las comisiones no pueden ser negativas." });
        }

        var fee = await _db.SellerFeeConfigurations.FirstOrDefaultAsync(x => x.SellerId == sellerId, cancellationToken);
        if (fee is null)
        {
            fee = new SellerFeeConfiguration
            {
                SellerId = sellerId,
                CreatedAt = DateTimeOffset.UtcNow
            };
            _db.SellerFeeConfigurations.Add(fee);
        }

        fee.FixedFeeAmount = request.FixedFeeAmount;
        fee.PercentageFee = request.PercentageFee;
        fee.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new { sellerId, percentageFee = fee.PercentageFee, fixedFeeAmount = fee.FixedFeeAmount });
    }
}
