using ComunaClick.Api.Persistence.Entities;

namespace ComunaClick.Api.Modules.Catalog;

public interface IStockNotificationService
{
    Task NotifyQuantityChangedAsync(
        Product product,
        int previousOnHand,
        int newOnHand,
        string reason,
        Guid? referenceId = null,
        CancellationToken cancellationToken = default);
}
