using ComunaClick.Api.Modules.Marketplace.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ComunaClick.Api.Modules.Marketplace;

[ApiController]
[Route("api/mercadopago/oauth")]
public sealed class MercadoPagoOAuthController : ControllerBase
{
    private readonly MercadoPagoOAuthService _oauthService;
    private readonly SellerMarketplaceService _sellerService;

    public MercadoPagoOAuthController(MercadoPagoOAuthService oauthService, SellerMarketplaceService sellerService)
    {
        _oauthService = oauthService;
        _sellerService = sellerService;
    }

    [HttpGet("start")]
    [Authorize(Policy = "partner.owner")]
    [EnableRateLimiting("public-write")]
    public async Task<ActionResult<MercadoPagoOAuthStartResponse>> Start([FromQuery] Guid sellerId, CancellationToken cancellationToken)
    {
        if (sellerId == Guid.Empty)
        {
            return BadRequest(new { message = "sellerId is required." });
        }

        if (!await _sellerService.UserCanManageSellerAsync(sellerId, cancellationToken))
        {
            return Forbid();
        }

        return Ok(await _oauthService.StartAsync(sellerId, cancellationToken));
    }

    [HttpGet("callback")]
    [AllowAnonymous]
    [EnableRateLimiting("public-read")]
    public async Task<IActionResult> Callback([FromQuery] string code, [FromQuery] string state, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
        {
            return BadRequest(new { message = "code and state are required." });
        }

        var redirectUrl = await _oauthService.CompleteAsync(code, state, cancellationToken);
        return Redirect(redirectUrl);
    }
}
