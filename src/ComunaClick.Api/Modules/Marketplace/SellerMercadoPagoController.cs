using ComunaClick.Api.Modules.Marketplace.Contracts;
using ComunaClick.Api.Persistence;
using ComunaClick.Api.Persistence.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace ComunaClick.Api.Modules.Marketplace;

[ApiController]
[Authorize(Policy = "partner.owner")]
[Route("api/sellers/{sellerId:guid}/mercadopago")]
public sealed class SellerMercadoPagoController : ControllerBase
{
    private readonly CoreDbContext _db;
    private readonly SellerMarketplaceService _sellerService;
    private readonly MercadoPagoOAuthService _oauthService;
    private readonly MarketplaceAuditService _auditService;

    public SellerMercadoPagoController(
        CoreDbContext db,
        SellerMarketplaceService sellerService,
        MercadoPagoOAuthService oauthService,
        MarketplaceAuditService auditService)
    {
        _db = db;
        _sellerService = sellerService;
        _oauthService = oauthService;
        _auditService = auditService;
    }

    [HttpGet("status")]
    [EnableRateLimiting("public-read")]
    public async Task<ActionResult<SellerMercadoPagoStatusResponse>> GetStatus(Guid sellerId, CancellationToken cancellationToken)
    {
        if (!await _sellerService.UserCanManageSellerAsync(sellerId, cancellationToken))
        {
            return Forbid();
        }

        var seller = await _sellerService.EnsureSellerAsync(sellerId, cancellationToken);
        var account = await _db.SellerMercadoPagoAccounts.AsNoTracking().FirstOrDefaultAsync(x => x.SellerId == sellerId, cancellationToken);
        var fee = await _db.SellerFeeConfigurations.AsNoTracking().FirstOrDefaultAsync(x => x.SellerId == sellerId, cancellationToken);

        return Ok(new SellerMercadoPagoStatusResponse(
            sellerId,
            seller.Name,
            account?.ConnectionStatus ?? "disconnected",
            account?.MpUserId,
            account?.Scope,
            account?.ConnectedAt,
            account?.TokenExpiresAt,
            new SellerFeeSettingsResponse(
                fee?.FixedFeeAmount ?? 0m,
                fee?.PercentageFee ?? 0m,
                fee?.IsActive ?? true)));
    }

    [HttpPost("disconnect")]
    [EnableRateLimiting("public-write")]
    public async Task<IActionResult> Disconnect(Guid sellerId, CancellationToken cancellationToken)
    {
        if (!await _sellerService.UserCanManageSellerAsync(sellerId, cancellationToken))
        {
            return Forbid();
        }

        await _oauthService.DisconnectAsync(sellerId, cancellationToken);
        return NoContent();
    }

    [HttpPut("/api/sellers/{sellerId:guid}/fees")]
    [EnableRateLimiting("public-write")]
    public async Task<ActionResult<SellerFeeSettingsResponse>> UpdateFees(Guid sellerId, [FromBody] UpdateSellerFeesRequest request, CancellationToken cancellationToken)
    {
        if (!await _sellerService.UserCanManageSellerAsync(sellerId, cancellationToken))
        {
            return Forbid();
        }

        if (request.FixedFeeAmount < 0 || request.PercentageFee < 0)
        {
            return BadRequest(new { message = "Fees cannot be negative." });
        }

        await _sellerService.EnsureSellerAsync(sellerId, cancellationToken);
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
        fee.IsActive = request.IsActive;
        fee.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        await _auditService.WriteAsync(
            _sellerService.GetActor(),
            "seller.fees.updated",
            nameof(SellerFeeConfiguration),
            sellerId.ToString(),
            request,
            cancellationToken);

        return Ok(new SellerFeeSettingsResponse(fee.FixedFeeAmount, fee.PercentageFee, fee.IsActive));
    }
}
