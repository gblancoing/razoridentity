using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ComunaClick.Api.Jobs;

public sealed class JobsHostedService : BackgroundService
{
    private readonly ILogger<JobsHostedService> _logger;
    private readonly IConfiguration _configuration;

    public JobsHostedService(ILogger<JobsHostedService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var enabled = _configuration.GetValue("Jobs:Enabled", false);
        if (!enabled)
        {
            _logger.LogInformation("JobsHostedService disabled.");
            return;
        }

        var intervalSeconds = Math.Max(10, _configuration.GetValue("Jobs:TickSeconds", 60));

        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("JobsHostedService tick at {Timestamp}.", DateTimeOffset.UtcNow);
            await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
        }
    }
}
