using ComunaClick.Api.Configuration;
using ComunaClick.Api.Modules.Orders;
using ComunaClick.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ComunaClick.Api.Jobs;

/// <summary>
/// Cancela formalmente las órdenes payment_pending cuya reserva venció
/// (Inventory:PendingOrderTtlMinutes). La reserva de stock ya se ignora en
/// tiempo real al vencer (ver ProductInventoryService); este job materializa
/// la cancelación para que comprador y vendedor vean el estado real.
///
/// Carrera pago-vs-expiración: el pago confirmado GANA.
/// - En proveedores relacionales la cancelación es un UPDATE condicional
///   (WHERE Status = payment_pending), atómico: si el webhook de MP marcó la
///   orden como paid primero, el job no la toca (0 filas afectadas).
/// - Si el job cancela primero y el pago llega después, el webhook repone el
///   estado paid (no pasa por la máquina de estados) y el descuento de stock
///   es idempotente (flag InventoryFulfilledAt bajo lock por producto).
/// Las órdenes pendientes nunca descontaron inventario físico, así que la
/// cancelación no repone stock: solo libera la reserva por estado.
/// </summary>
public sealed class PendingOrderExpirationJob
{
    private const int BatchSize = 100;

    private readonly CoreDbContext _db;
    private readonly InventoryOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PendingOrderExpirationJob> _logger;

    public PendingOrderExpirationJob(
        CoreDbContext db,
        IOptions<InventoryOptions> options,
        ILogger<PendingOrderExpirationJob> logger,
        TimeProvider? timeProvider = null)
    {
        _db = db;
        _options = options.Value;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var ttlMinutes = _options.PendingOrderTtlMinutes;
        if (ttlMinutes <= 0)
        {
            return 0;
        }

        var now = _timeProvider.GetUtcNow();
        var cutoff = now.AddMinutes(-ttlMinutes);

        var expiredIds = await _db.Orders
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.Status == OrderStatusMachine.PaymentPending && x.CreatedAt < cutoff)
            .OrderBy(x => x.CreatedAt)
            .Select(x => x.Id)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (expiredIds.Count == 0)
        {
            return 0;
        }

        var cancelled = 0;
        foreach (var orderId in expiredIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_db.Database.IsRelational())
            {
                // UPDATE condicional: si entre el SELECT y este punto la orden
                // pasó a paid (webhook MP), el WHERE no matchea y no se cancela.
                cancelled += await _db.Orders
                    .IgnoreQueryFilters()
                    .Where(x => x.Id == orderId && x.Status == OrderStatusMachine.PaymentPending)
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(x => x.Status, OrderStatusMachine.Cancelled)
                            .SetProperty(x => x.UpdatedAt, now),
                        cancellationToken);
            }
            else
            {
                // Proveedor InMemory (tests): re-chequeo + save por entidad.
                var order = await _db.Orders
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(x => x.Id == orderId, cancellationToken);
                if (order is null || !string.Equals(order.Status, OrderStatusMachine.PaymentPending, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                order.Status = OrderStatusMachine.Cancelled;
                order.UpdatedAt = now;
                await _db.SaveChangesAsync(cancellationToken);
                cancelled++;
            }
        }

        if (cancelled > 0)
        {
            _logger.LogInformation(
                "Expiración de pedidos pendientes: {Cancelled} orden(es) canceladas por superar {TtlMinutes} min sin pago.",
                cancelled,
                ttlMinutes);
        }

        return cancelled;
    }
}
