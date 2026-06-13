using ComunaClick.Api.Modules.Checkout.Contracts;
using ComunaClick.Api.Modules.Delivery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ComunaClick.Api.Modules.Checkout;

[ApiController]
[Route("v1/public/checkout")]
public sealed class PublicCheckoutController : ControllerBase
{
    private readonly IBuyerCheckoutPaymentService _checkoutPayments;
    private readonly IDeliveryQuoteService _deliveryQuotes;

    public PublicCheckoutController(
        IBuyerCheckoutPaymentService checkoutPayments,
        IDeliveryQuoteService deliveryQuotes)
    {
        _checkoutPayments = checkoutPayments;
        _deliveryQuotes = deliveryQuotes;
    }

    [AllowAnonymous]
    [EnableRateLimiting("public-read")]
    [HttpGet("delivery-quote")]
    public async Task<ActionResult<PublicDeliveryQuoteResponse>> GetDeliveryQuote(
        [FromQuery] Guid partnerId,
        [FromQuery] double? destinationLat,
        [FromQuery] double? destinationLng,
        CancellationToken cancellationToken)
    {
        if (partnerId == Guid.Empty)
        {
            return BadRequest(new { message = "partnerId is required." });
        }

        var quote = await _deliveryQuotes.GetQuoteAsync(partnerId, destinationLat, destinationLng, cancellationToken);
        if (!quote.Available)
        {
            return NotFound(new { message = quote.Message ?? "Partner not found." });
        }

        return Ok(quote);
    }

    [AllowAnonymous]
    [EnableRateLimiting("public-read")]
    [HttpGet("partners/{partnerId:guid}/payment-status")]
    public async Task<ActionResult<PublicPartnerPaymentStatusResponse>> GetPartnerPaymentStatus(
        Guid partnerId,
        CancellationToken cancellationToken)
        => Ok(await _checkoutPayments.GetPartnerPaymentStatusAsync(partnerId, cancellationToken));

    [AllowAnonymous]
    [EnableRateLimiting("public-write")]
    [HttpPost("mercadopago")]
    public async Task<ActionResult<PublicMercadoPagoCheckoutResponse>> CreateMercadoPagoCheckout(
        PublicMercadoPagoCheckoutRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _checkoutPayments.CreateMercadoPagoCheckoutAsync(request, cancellationToken);
        if (!response.Available)
        {
            return BadRequest(new { message = response.Message ?? "Checkout is not available." });
        }

        return Ok(response);
    }

    [AllowAnonymous]
    [EnableRateLimiting("public-write")]
    [HttpPost("mercadopago/resume")]
    public async Task<ActionResult<PublicMercadoPagoCheckoutResponse>> ResumeMercadoPagoCheckout(
        PublicMercadoPagoCheckoutRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _checkoutPayments.ResumeMercadoPagoCheckoutAsync(request, cancellationToken);
        if (!response.Available)
        {
            return BadRequest(new { message = response.Message ?? "Checkout is not available." });
        }

        return Ok(response);
    }

    [AllowAnonymous]
    [EnableRateLimiting("public-write")]
    [HttpPost("payment-reminder")]
    public async Task<IActionResult> SendPaymentReminder(
        PublicPaymentReminderRequest request,
        CancellationToken cancellationToken)
    {
        await _checkoutPayments.SendPaymentReminderAsync(request, cancellationToken);
        return Accepted();
    }
}
