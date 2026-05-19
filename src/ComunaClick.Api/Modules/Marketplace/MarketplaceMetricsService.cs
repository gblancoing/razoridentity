using System.Collections.Concurrent;

namespace ComunaClick.Api.Modules.Marketplace;

public sealed class MarketplaceMetricsService
{
    private readonly ConcurrentDictionary<string, long> _counters = new(StringComparer.OrdinalIgnoreCase);

    public void Increment(string metricName)
    {
        _counters.AddOrUpdate(metricName, 1, (_, current) => current + 1);
    }

    public IReadOnlyDictionary<string, long> Snapshot()
        => new Dictionary<string, long>(_counters, StringComparer.OrdinalIgnoreCase);
}
