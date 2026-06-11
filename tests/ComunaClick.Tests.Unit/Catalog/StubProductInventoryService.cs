using ComunaClick.Api.Modules.Catalog;
using ComunaClick.Api.Persistence.Entities;

namespace ComunaClick.Tests.Unit.Catalog;

internal sealed class StubProductInventoryService : IProductInventoryService
{
    public Task<int> GetAvailableQuantityAsync(Guid productId, Guid? excludeOrderId = null, CancellationToken cancellationToken = default)
        => Task.FromResult(0);

    public Task<IReadOnlyDictionary<Guid, int>> GetAvailableQuantitiesAsync(
        IReadOnlyCollection<Guid> productIds,
        Guid? excludeOrderId = null,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyDictionary<Guid, int>>(new Dictionary<Guid, int>());

    public Task<(bool Ok, string? Message)> ValidateLineItemsAsync(
        IReadOnlyCollection<(Guid ProductId, int Quantity)> lines,
        Guid? excludeOrderId = null,
        CancellationToken cancellationToken = default)
        => Task.FromResult<(bool, string?)>((true, null));

    public Task FulfillOrderAsync(Order order, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task EnsureInventoryRowAsync(Guid productId, int initialQuantity, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task NotifyStockLevelsAsync(
        Product product,
        int previousOnHand,
        int newOnHand,
        string reason,
        Guid? referenceId = null,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
