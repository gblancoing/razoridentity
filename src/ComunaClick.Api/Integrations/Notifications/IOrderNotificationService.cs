using ComunaClick.Api.Persistence.Entities;

namespace ComunaClick.Api.Integrations.Notifications;

/// <summary>
/// Encola notificaciones en el outbox (no envía dentro del request). El envío real,
/// con reintentos y backoff, lo realiza <see cref="NotificationOutboxProcessor"/>.
/// </summary>
public interface IOrderNotificationService
{
    /// <summary>Confirmación de compra al comprador (se encola al crear la orden).</summary>
    Task NotifyBuyerOrderAsync(Order order, Partner partner, Customer customer, CancellationToken cancellationToken = default);

    /// <summary>
    /// Aviso de venta al comercio. Solo debe llamarse cuando la orden está pagada.
    /// Es idempotente: no encola dos veces para la misma orden.
    /// </summary>
    Task NotifyPartnerOrderPaidAsync(Guid orderId, CancellationToken cancellationToken = default);

    Task NotifyBuyerBookingAsync(Booking booking, Partner partner, Customer customer, string? serviceName, CancellationToken cancellationToken = default);

    Task NotifyBuyerPaymentPendingAsync(Order order, Partner partner, Customer customer, CancellationToken cancellationToken = default);

    Task NotifyBuyerBookingPaymentPendingAsync(
        Booking booking,
        Partner partner,
        Customer customer,
        string? serviceName,
        CancellationToken cancellationToken = default);
}
