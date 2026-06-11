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

    public AdminMarketplaceController(CoreDbContext db)
    {
        _db = db;
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
