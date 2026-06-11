using ComunaClick.Api.Modules.Checkout.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ComunaClick.Api.Modules.Checkout;

[ApiController]
[Route("v1/public/checkout")]
public sealed class PublicCheckoutController : ControllerBase
{
    private readonly IBuyerCheckoutPaymentService _checkoutPayments;

    public PublicCheckoutController(IBuyerCheckoutPaymentService checkoutPayments)
    {
        _checkoutPayments = checkoutPayments;
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
