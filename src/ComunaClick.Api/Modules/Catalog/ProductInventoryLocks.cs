using System.Collections.Concurrent;

namespace ComunaClick.Api.Modules.Catalog;

/// <summary>
/// Serializa las operaciones de stock por producto dentro del proceso. Se adquieren
/// en orden estable (por GUID) para evitar deadlocks cuando una orden toca varios productos.
/// </summary>
public static class ProductInventoryLocks
{
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> Locks = new();

    public static async Task<IAsyncDisposable> AcquireAsync(
        IEnumerable<Guid> productIds,
        CancellationToken cancellationToken = default)
    {
        var ordered = productIds.Distinct().OrderBy(x => x).ToList();
        var acquired = new List<SemaphoreSlim>(ordered.Count);

        try
        {
            foreach (var productId in ordered)
            {
                var gate = Locks.GetOrAdd(productId, _ => new SemaphoreSlim(1, 1));
                await gate.WaitAsync(cancellationToken);
                acquired.Add(gate);
            }
        }
        catch
        {
            ReleaseAll(acquired);
            throw;
        }

        return new Releaser(acquired);
    }

    private static void ReleaseAll(List<SemaphoreSlim> acquired)
    {
        for (var i = acquired.Count - 1; i >= 0; i--)
        {
            acquired[i].Release();
        }
    }

    private sealed class Releaser : IAsyncDisposable
    {
        private List<SemaphoreSlim>? _acquired;

        public Releaser(List<SemaphoreSlim> acquired) => _acquired = acquired;

        public ValueTask DisposeAsync()
        {
            var acquired = Interlocked.Exchange(ref _acquired, null);
            if (acquired is not null)
            {
                ReleaseAll(acquired);
            }

            return ValueTask.CompletedTask;
        }
    }
}
