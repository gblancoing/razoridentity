using ComunaClick.Api.Persistence.Entities;

namespace ComunaClick.Api.Integrations.Notifications;

public interface IOrderNotificationService
{
    Task NotifyPartnerAsync(Order order, Partner partner, Customer customer, IReadOnlyList<OrderItem> items, CancellationToken cancellationToken = default);
}
