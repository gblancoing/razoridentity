using ComunaClick.Api.Modules.Marketplace.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ComunaClick.Api.Modules.Marketplace;

[ApiController]
[Route("api/payments")]
public sealed class MarketplacePaymentsController : ControllerBase
{
    private readonly MarketplacePaymentService _paymentService;
    private readonly SellerMarketplaceService _sellerService;
    private readonly MercadoPagoWebhookService _webhookService;

    public MarketplacePaymentsController(
        MarketplacePaymentService paymentService,
        SellerMarketplaceService sellerService,
        MercadoPagoWebhookService webhookService)
    {
        _paymentService = paymentService;
        _sellerService = sellerService;
        _webhookService = webhookService;
    }

    [HttpPost("create")]
    [Authorize(Policy = "partner.owner")]
    [EnableRateLimiting("public-write")]
    public async Task<ActionResult<MarketplacePaymentResponse>> Create([FromBody] CreateMarketplacePaymentRequest request, CancellationToken cancellationToken)
    {
        if (!await _sellerService.UserCanManageSellerAsync(request.SellerId, cancellationToken))
        {
            return Forbid();
        }

        var response = await _paymentService.CreateAsync(request, cancellationToken);
        return Ok(response);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "partner.owner")]
    [EnableRateLimiting("public-read")]
    public async Task<ActionResult<MarketplacePaymentDetailResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        var sellerId = await _paymentService.GetSellerIdByPaymentAsync(id, cancellationToken);
        if (!sellerId.HasValue || sellerId == Guid.Empty)
        {
            return NotFound();
        }

        if (!await _sellerService.UserCanManageSellerAsync(sellerId.Value, cancellationToken))
        {
            return Forbid();
        }

        var item = await _paymentService.GetAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("order/{orderId:guid}")]
    [Authorize(Policy = "partner.owner")]
    [EnableRateLimiting("public-read")]
    public async Task<ActionResult<MarketplacePaymentDetailResponse>> GetByOrder(Guid orderId, CancellationToken cancellationToken)
    {
        var sellerId = await _paymentService.GetSellerIdByOrderAsync(orderId, cancellationToken);
        if (!sellerId.HasValue || sellerId == Guid.Empty)
        {
            return NotFound();
        }

        if (!await _sellerService.UserCanManageSellerAsync(sellerId.Value, cancellationToken))
        {
            return Forbid();
        }

        var item = await _paymentService.GetByOrderAsync(orderId, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("/api/sellers/{sellerId:guid}/payments")]
    [Authorize(Policy = "partner.owner")]
    [EnableRateLimiting("public-read")]
    public async Task<ActionResult<IReadOnlyList<MarketplacePaymentDetailResponse>>> ListBySeller(Guid sellerId, CancellationToken cancellationToken)
    {
        if (!await _sellerService.UserCanManageSellerAsync(sellerId, cancellationToken))
        {
            return Forbid();
        }

        return Ok(await _paymentService.ListBySellerAsync(sellerId, cancellationToken));
    }

    [HttpPost("{id:guid}/sync")]
    [Authorize(Policy = "partner.owner")]
    [EnableRateLimiting("public-write")]
    public async Task<IActionResult> RetrySync(Guid id, CancellationToken cancellationToken)
    {
        var sellerId = await _paymentService.GetSellerIdByPaymentAsync(id, cancellationToken);
        if (!sellerId.HasValue || sellerId == Guid.Empty)
        {
            return NotFound();
        }

        if (!await _sellerService.UserCanManageSellerAsync(sellerId.Value, cancellationToken))
        {
            return Forbid();
        }

        await _webhookService.SyncPaymentAsync(id, cancellationToken);
        return Accepted();
    }
}
