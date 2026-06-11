using ComunaClick.Api.Integrations.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ComunaClick.Api.Jobs;

/// <summary>
/// Worker que vacía el outbox de notificaciones de forma periódica, fuera del request.
/// Solo corre si las notificaciones están habilitadas.
/// </summary>
public sealed class NotificationOutboxHostedService : BackgroundService
{
    private readonly ILogger<NotificationOutboxHostedService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly OrderNotificationOptions _options;

    public NotificationOutboxHostedService(
        ILogger<NotificationOutboxHostedService> logger,
        IServiceScopeFactory scopeFactory,
        IOptions<OrderNotificationOptions> options)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("NotificationOutboxHostedService disabled (OrderNotifications:Enabled=false).");
            return;
        }

        var intervalSeconds = Math.Max(5, _options.WorkerIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<NotificationOutboxProcessor>();
                await processor.ProcessPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Notification outbox tick failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
        }
    }
}
