using ComunaClick.Api.Modules.Marketplace;
using ComunaClick.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ComunaClick.Api.Jobs;

public sealed class PaymentReconciliationJob
{
    private readonly CoreDbContext _db;
    private readonly MercadoPagoWebhookService _webhookService;
    private readonly ILogger<PaymentReconciliationJob> _logger;

    public PaymentReconciliationJob(CoreDbContext db, MercadoPagoWebhookService webhookService, ILogger<PaymentReconciliationJob> logger)
    {
        _db = db;
        _webhookService = webhookService;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var minAge = now.AddMinutes(-5);
        var maxAge = now.AddDays(-7);
        var statuses = new[] { "pending", "in_process", "authorized" };

        var stuckIds = await _db.Payments
            .Where(p => p.Provider == "mercadopago" && statuses.Contains(p.Status)
                && p.CreatedAt <= minAge && p.CreatedAt >= maxAge)
            .OrderBy(p => p.CreatedAt)
            .Take(100)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        foreach (var id in stuckIds)
        {
            try
            {
                await _webhookService.SyncPaymentAsync(id, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Reconciliation failed for payment {PaymentId}", id);
            }
        }

        if (stuckIds.Count > 0)
        {
            _logger.LogInformation("Reconciliation processed {Count} stuck payments.", stuckIds.Count);
        }
    }
}
