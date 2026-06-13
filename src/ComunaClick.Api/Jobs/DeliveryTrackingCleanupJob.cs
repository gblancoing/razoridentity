using ComunaClick.Api.Configuration;
using ComunaClick.Api.Modules.Delivery;
using ComunaClick.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ComunaClick.Api.Jobs;

/// <summary>
/// Retención del historial GPS: para envíos terminados (delivered/canceled)
/// hace más de N días, borra el historial de delivery_tracking conservando
/// el último punto por orden (para que el snapshot siga teniendo posición).
/// </summary>
public sealed class DeliveryTrackingCleanupJob
{
    private readonly CoreDbContext _db;
    private readonly DeliveryOptions _options;
    private readonly ILogger<DeliveryTrackingCleanupJob> _logger;

    public DeliveryTrackingCleanupJob(
        CoreDbContext db,
        IOptions<DeliveryOptions> options,
        ILogger<DeliveryTrackingCleanupJob> logger)
    {
        _db = db;
        _options = options.Value;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-Math.Max(1, _options.TrackingRetentionDays));

        var finishedOrderIds = await _db.Orders.AsNoTracking()
            .Where(x => (x.DeliveryStatus == DeliveryStatuses.Delivered || x.DeliveryStatus == DeliveryStatuses.Canceled)
                && x.UpdatedAt <= cutoff)
            .Where(x => _db.DeliveryTrackings.Count(t => t.OrderId == x.Id) > 1)
            .Select(x => x.Id)
            .Take(50)
            .ToListAsync(cancellationToken);

        var removedTotal = 0;
        foreach (var orderId in finishedOrderIds)
        {
            // Se conserva la última fila (posición final del reparto).
            var lastId = await _db.DeliveryTrackings.AsNoTracking()
                .Where(x => x.OrderId == orderId)
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => x.Id)
                .FirstAsync(cancellationToken);

            var stale = await _db.DeliveryTrackings
                .Where(x => x.OrderId == orderId && x.Id != lastId)
                .ToListAsync(cancellationToken);

            _db.DeliveryTrackings.RemoveRange(stale);
            removedTotal += stale.Count;
        }

        if (removedTotal > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "Delivery tracking cleanup: {Rows} puntos eliminados en {Orders} envíos.",
                removedTotal,
                finishedOrderIds.Count);
        }
    }
}
