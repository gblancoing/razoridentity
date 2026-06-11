using ComunaClick.Api.Persistence.Entities;

namespace ComunaClick.Api.Integrations.Notifications;

public interface IOrderNotificationService
{
    Task NotifyPartnerAsync(Order order, Partner partner, Customer customer, IReadOnlyList<OrderItem> items, CancellationToken cancellationToken = default);

    Task NotifyBuyerOrderAsync(Order order, Partner partner, Customer customer, CancellationToken cancellationToken = default);

    Task NotifyBuyerBookingAsync(Booking booking, Partner partner, Customer customer, string? serviceName, CancellationToken cancellationToken = default);

    Task NotifyBuyerPaymentPendingAsync(Order order, Partner partner, Customer customer, CancellationToken cancellationToken = default);

    Task NotifyBuyerBookingPaymentPendingAsync(
        Booking booking,
        Partner partner,
        Customer customer,
        string? serviceName,
        CancellationToken cancellationToken = default);
}
