using ComunaClick.Api.Persistence.Entities;

namespace ComunaClick.Api.Modules.Catalog;

public interface IProductInventoryService
{
    Task<int> GetAvailableQuantityAsync(Guid productId, Guid? excludeOrderId = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, int>> GetAvailableQuantitiesAsync(
        IReadOnlyCollection<Guid> productIds,
        Guid? excludeOrderId = null,
        CancellationToken cancellationToken = default);

    Task<(bool Ok, string? Message)> ValidateLineItemsAsync(
        IReadOnlyCollection<(Guid ProductId, int Quantity)> lines,
        Guid? excludeOrderId = null,
        CancellationToken cancellationToken = default);

    Task FulfillOrderAsync(Order order, CancellationToken cancellationToken = default);

    Task RestoreOrderAsync(Order order, CancellationToken cancellationToken = default);

    Task EnsureInventoryRowAsync(Guid productId, int initialQuantity, CancellationToken cancellationToken = default);

    Task NotifyStockLevelsAsync(
        Product product,
        int previousOnHand,
        int newOnHand,
        string reason,
        Guid? referenceId = null,
        CancellationToken cancellationToken = default);
}
