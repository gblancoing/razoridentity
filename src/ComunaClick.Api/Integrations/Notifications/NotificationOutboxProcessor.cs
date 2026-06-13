using ComunaClick.Api.Persistence;
using ComunaClick.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ComunaClick.Api.Integrations.Notifications;

/// <summary>
/// Procesa el outbox de notificaciones: toma pendientes vencidas, las envía por su canal y
/// marca el resultado. Ante fallo, incrementa intentos y reprograma con backoff exponencial;
/// al agotar los reintentos la deja en estado terminal "failed" (no se pierde el registro).
/// </summary>
public sealed class NotificationOutboxProcessor
{
    private readonly CoreDbContext _db;
    private readonly IEmailSender _emailSender;
    private readonly IWhatsAppSender _whatsAppSender;
    private readonly OrderNotificationOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<NotificationOutboxProcessor> _logger;

    public NotificationOutboxProcessor(
        CoreDbContext db,
        IEmailSender emailSender,
        IWhatsAppSender whatsAppSender,
        IOptions<OrderNotificationOptions> options,
        TimeProvider timeProvider,
        ILogger<NotificationOutboxProcessor> logger)
    {
        _db = db;
        _emailSender = emailSender;
        _whatsAppSender = whatsAppSender;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <returns>Cantidad de notificaciones procesadas (enviadas o reintentadas) en este tick.</returns>
    public async Task<int> ProcessPendingAsync(CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();
        var batchSize = Math.Max(1, _options.OutboxBatchSize);

        var pending = await _db.NotificationOutbox
            .IgnoreQueryFilters()
            .Where(x => x.Status == "pending" && x.NextAttemptAt <= now)
            .OrderBy(x => x.NextAttemptAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        if (pending.Count == 0)
        {
            return 0;
        }

        foreach (var item in pending)
        {
            await ProcessOneAsync(item, cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return pending.Count;
    }

    private async Task ProcessOneAsync(NotificationOutbox item, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        try
        {
            await SendAsync(item, cancellationToken);
            item.Status = "sent";
            item.SentAt = now;
            item.LastError = null;
            item.Attempts += 1;
            item.UpdatedAt = now;
        }
        catch (Exception ex)
        {
            item.Attempts += 1;
            item.LastError = ex.Message;
            item.UpdatedAt = now;

            if (item.Attempts >= item.MaxAttempts)
            {
                item.Status = "failed";
                _logger.LogError(
                    ex,
                    "Notificación {Id} ({Kind}/{Channel}) agotó {Attempts} reintentos y queda en failed.",
                    item.Id, item.Kind, item.Channel, item.Attempts);
            }
            else
            {
                item.Status = "pending";
                item.NextAttemptAt = now + ComputeBackoff(item.Attempts);
                _logger.LogWarning(
                    ex,
                    "Notificación {Id} ({Kind}/{Channel}) falló en intento {Attempts}; reintento programado {NextAttemptAt:o}.",
                    item.Id, item.Kind, item.Channel, item.Attempts, item.NextAttemptAt);
            }
        }
    }

    private Task SendAsync(NotificationOutbox item, CancellationToken cancellationToken)
    {
        if (string.Equals(item.Channel, "whatsapp", StringComparison.OrdinalIgnoreCase))
        {
            var payload = string.IsNullOrWhiteSpace(item.PayloadJson) ? item.Body : item.PayloadJson;
            return _whatsAppSender.SendAsync(payload, cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(item.Recipient))
        {
            throw new InvalidOperationException("La notificación de correo no tiene destinatario.");
        }

        return _emailSender.SendAsync(item.Recipient, item.Subject ?? string.Empty, item.Body, cancellationToken);
    }

    private TimeSpan ComputeBackoff(int attempts)
    {
        var baseSeconds = Math.Max(0, _options.OutboxRetryBaseSeconds);
        if (baseSeconds == 0)
        {
            return TimeSpan.Zero;
        }

        // Backoff exponencial acotado a 1 hora.
        var exponent = Math.Min(attempts - 1, 16);
        var seconds = Math.Min(baseSeconds * Math.Pow(2, exponent), TimeSpan.FromHours(1).TotalSeconds);
        return TimeSpan.FromSeconds(seconds);
    }
}
