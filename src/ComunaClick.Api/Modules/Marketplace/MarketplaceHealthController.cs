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
        return Ok(new
        {
            status = "ok",
            connectedSellers,
            pendingWebhooks,
            metrics = _metrics.Snapshot()
        });
    }
}
