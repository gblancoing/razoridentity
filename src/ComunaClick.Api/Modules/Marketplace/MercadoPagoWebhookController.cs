using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ComunaClick.Api.Modules.Marketplace;

[ApiController]
[Route("api/webhooks/mercadopago")]
public sealed class MercadoPagoWebhookController : ControllerBase
{
    private readonly MercadoPagoWebhookService _webhookService;

    public MercadoPagoWebhookController(MercadoPagoWebhookService webhookService)
    {
        _webhookService = webhookService;
    }

    [HttpPost]
    [AllowAnonymous]
    [EnableRateLimiting("webhook")]
    public async Task<IActionResult> Post(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync(cancellationToken);
        var result = await _webhookService.HandleAsync(Request, string.IsNullOrWhiteSpace(payload) ? "{}" : payload, cancellationToken);
        return Ok(new
        {
            result.Id,
            result.Processed,
            result.SignatureValid
        });
    }
}
