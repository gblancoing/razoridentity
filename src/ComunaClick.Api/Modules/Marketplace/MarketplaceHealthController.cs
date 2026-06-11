using ComunaClick.Api.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ComunaClick.Api.Modules.Marketplace;

[ApiController]
[Authorize(Policy = "tenant.admin")]
[Route("api/marketplace")]
public sealed class MarketplaceHealthController : ControllerBase
{
    private readonly CoreDbContext _db;
    private readonly MarketplaceMetricsService _metrics;

    public MarketplaceHealthController(CoreDbContext db, MarketplaceMetricsService metrics)
    {
        _db = db;
        _metrics = metrics;
    }

    [HttpGet("health")]
    public async Task<IActionResult> GetHealth(CancellationToken cancellationToken)
    {
        var connectedSellers = await _db.SellerMercadoPagoAccounts.CountAsync(x => x.ConnectionStatus == "connected", cancellationToken);
        var pendingWebhooks = await _db.WebhookEvents.CountAsync(x => !x.Processed, cancellationToken);

        var stuckThreshold = DateTimeOffset.UtcNow.AddMinutes(-30);
        var stuckPendingPayments = await _db.Payments.CountAsync(
            x => x.Provider == "mercadopago"
              && (x.Status == "pending" || x.Status == "in_process" || x.Status == "authorized")
              && x.CreatedAt <= stuckThreshold,
            cancellationToken);

        return Ok(new
        {
            status = "ok",
            connectedSellers,
            pendingWebhooks,
            stuckPendingPayments,
            metrics = _metrics.Snapshot()
        });
    }
}
