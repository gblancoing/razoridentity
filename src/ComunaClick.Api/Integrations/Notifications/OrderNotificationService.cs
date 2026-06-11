using System.Text.Json;
using ComunaClick.Api.Modules.Orders;
using ComunaClick.Api.Persistence;
using ComunaClick.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ComunaClick.Api.Integrations.Notifications;

public sealed class OrderNotificationService : IOrderNotificationService
{
    private readonly OrderNotificationOptions _options;
    private readonly CoreDbContext _db;
    private readonly IOrderTrackingTokenService _trackingTokens;
    private readonly ILogger<OrderNotificationService> _logger;

    public OrderNotificationService(
        IOptions<OrderNotificationOptions> options,
        CoreDbContext db,
        IOrderTrackingTokenService trackingTokens,
        ILogger<OrderNotificationService> logger)
    {
        _options = options.Value;
        _db = db;
        _trackingTokens = trackingTokens;
        _logger = logger;
    }

    public async Task NotifyPartnerOrderPaidAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        if (orderId == Guid.Empty)
        {
            return;
        }

        // Idempotencia: si ya se encoló el aviso de venta para esta orden, no duplicar
        // (el pago puede confirmarse por varias vías: webhook MP, provider-notify, panel).
        var alreadyQueued = await _db.NotificationOutbox
            .IgnoreQueryFilters()
            .AnyAsync(x => x.ReferenceId == orderId && x.Kind == NotificationKinds.PartnerOrderPaid, cancellationToken);
        if (alreadyQueued)
        {
            return;
        }

        var order = await _db.Orders
            .IgnoreQueryFilters()
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == orderId, cancellationToken);
        if (order is null)
        {
            return;
        }

        var partner = await _db.Partners.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == order.PartnerId, cancellationToken);
        var customer = await _db.Customers.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == order.CustomerId, cancellationToken);
        if (partner is null || customer is null)
        {
            return;
        }

        if (!_options.Enabled)
        {
            await SaveNotificationAuditAsync(order, partner, customer, "disabled", "notifications_disabled", cancellationToken);
            return;
        }

        var message = BuildPartnerMessage(order, partner, customer, order.Items);
        var enqueued = false;

        if (_options.EnableEmail && !string.IsNullOrWhiteSpace(partner.Email))
        {
            Enqueue(
                order.TenantId,
                "email",
                NotificationKinds.PartnerOrderPaid,
                order.Id,
                partner.Email!.Trim(),
                "Nueva venta pagada - ComunaClic",
                message,
                payloadJson: null);
            enqueued = true;
        }

        if (_options.EnableWhatsAppWebhook && !string.IsNullOrWhiteSpace(partner.Phone))
        {
            var payload = JsonSerializer.Serialize(new
            {
                channel = "whatsapp",
                orderId = order.Id,
                partnerId = partner.Id,
                to = partner.Phone,
                customer = customer.FullName ?? customer.Email ?? customer.Id.ToString(),
                message
            });
            Enqueue(order.TenantId, "whatsapp", NotificationKinds.PartnerOrderPaid, order.Id, partner.Phone, null, message, payload);
            enqueued = true;
        }

        await SaveNotificationAuditAsync(
            order,
            partner,
            customer,
            enqueued ? "queued" : "no_channel_enabled",
            enqueued ? "partner_order_paid" : "no_channel_enabled",
            cancellationToken);
    }

    public async Task NotifyBuyerOrderAsync(Order order, Partner partner, Customer customer, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || !_options.EnableEmail || !_options.EnableBuyerEmail)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(customer.Email))
        {
            return;
        }

        var baseUrl = _options.AppBaseUrl.TrimEnd('/');
        var token = _trackingTokens.Create(order.Id, order.CustomerId);
        var trackingUrl = $"{baseUrl}/buyer/orders?orderId={order.Id}&token={token}";
        var message = new List<string>
        {
            "Hola" + (string.IsNullOrWhiteSpace(customer.FullName) ? "" : $" {customer.FullName.Trim()}") + ",",
            string.Empty,
            $"Tu compra en {partner.Name} quedó registrada.",
            $"Orden: #{order.Id}",
            $"Total: {order.TotalAmount:N0} {order.Currency}",
            $"Estado: {order.Status}",
            string.Empty,
            "Seguimiento:",
            trackingUrl,
            string.Empty,
            "Si el negocio tiene pago en línea, también podés completarlo desde el enlace de seguimiento.",
            string.Empty,
            "— ComunaClic"
        };

        Enqueue(
            order.TenantId,
            "email",
            NotificationKinds.BuyerOrder,
            order.Id,
            customer.Email.Trim(),
            "Tu compra en ComunaClic — seguimiento",
            string.Join(Environment.NewLine, message),
            payloadJson: null);

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task NotifyBuyerBookingAsync(
        Booking booking,
        Partner partner,
        Customer customer,
        string? serviceName,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || !_options.EnableEmail || !_options.EnableBuyerEmail)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(customer.Email))
        {
            return;
        }

        var baseUrl = _options.AppBaseUrl.TrimEnd('/');
        var trackingUrl = $"{baseUrl}/buyer/orders?bookingId={booking.Id}&customerId={booking.CustomerId}";
        var label = string.IsNullOrWhiteSpace(serviceName) ? "tu reserva" : serviceName.Trim();
        var message = new List<string>
        {
            "Hola" + (string.IsNullOrWhiteSpace(customer.FullName) ? "" : $" {customer.FullName.Trim()}") + ",",
            string.Empty,
            $"Tu reserva de {label} en {partner.Name} quedó registrada.",
            $"Reserva: #{booking.Id}",
            $"Horario: {booking.StartAt:dd/MM/yyyy HH:mm} — {booking.EndAt:HH:mm}",
            $"Monto: {booking.Amount:N0} {booking.Currency}",
            $"Estado: {booking.Status}",
            string.Empty,
            "Seguimiento:",
            trackingUrl,
            string.Empty,
            "— ComunaClic"
        };

        Enqueue(
            booking.TenantId,
            "email",
            NotificationKinds.BuyerBooking,
            booking.Id,
            customer.Email.Trim(),
            "Tu reserva en ComunaClic — seguimiento",
            string.Join(Environment.NewLine, message),
            payloadJson: null);

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task NotifyBuyerPaymentPendingAsync(Order order, Partner partner, Customer customer, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(customer.Email))
        {
            return;
        }

        var baseUrl = _options.AppBaseUrl.TrimEnd('/');
        var token = _trackingTokens.Create(order.Id, order.CustomerId);
        var trackingUrl = $"{baseUrl}/buyer/orders?orderId={order.Id}&token={token}";
        var message = new List<string>
        {
            "Hola" + (string.IsNullOrWhiteSpace(customer.FullName) ? "" : $" {customer.FullName.Trim()}") + ",",
            string.Empty,
            $"Tienes un pago pendiente en {partner.Name} por tu compra #{order.Id}.",
            $"Total: {order.TotalAmount:N0} {order.Currency}",
            string.Empty,
            "Completa el pago o revisa el estado aquí:",
            trackingUrl,
            string.Empty,
            "— ComunaClic"
        };

        Enqueue(
            order.TenantId,
            "email",
            NotificationKinds.BuyerPaymentPending,
            order.Id,
            customer.Email.Trim(),
            "Recordatorio: completa tu pago en ComunaClic",
            string.Join(Environment.NewLine, message),
            payloadJson: null);

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task NotifyBuyerBookingPaymentPendingAsync(
        Booking booking,
        Partner partner,
        Customer customer,
        string? serviceName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(customer.Email))
        {
            return;
        }

        var baseUrl = _options.AppBaseUrl.TrimEnd('/');
        var trackingUrl = $"{baseUrl}/buyer/orders?bookingId={booking.Id}&customerId={booking.CustomerId}";
        var label = string.IsNullOrWhiteSpace(serviceName) ? "tu reserva" : serviceName.Trim();
        var message = new List<string>
        {
            "Hola" + (string.IsNullOrWhiteSpace(customer.FullName) ? "" : $" {customer.FullName.Trim()}") + ",",
            string.Empty,
            $"Tienes un pago pendiente para {label} en {partner.Name}.",
            $"Reserva: #{booking.Id}",
            string.Empty,
            "Completa el pago o revisa el estado aquí:",
            trackingUrl,
            string.Empty,
            "— ComunaClic"
        };

        Enqueue(
            booking.TenantId,
            "email",
            NotificationKinds.BuyerBookingPaymentPending,
            booking.Id,
            customer.Email.Trim(),
            "Recordatorio: completa el pago de tu reserva",
            string.Join(Environment.NewLine, message),
            payloadJson: null);

        await _db.SaveChangesAsync(cancellationToken);
    }

    private void Enqueue(
        Guid tenantId,
        string channel,
        string kind,
        Guid? referenceId,
        string? recipient,
        string? subject,
        string body,
        string? payloadJson)
    {
        var now = DateTimeOffset.UtcNow;
        _db.NotificationOutbox.Add(new NotificationOutbox
        {
            TenantId = tenantId,
            Channel = channel,
            Kind = kind,
            ReferenceId = referenceId,
            Recipient = recipient,
            Subject = subject,
            Body = body,
            PayloadJson = payloadJson,
            Status = "pending",
            Attempts = 0,
            MaxAttempts = Math.Max(1, _options.OutboxMaxAttempts),
            NextAttemptAt = now,
            CreatedAt = now,
            UpdatedAt = now
        });
    }

    private async Task SaveNotificationAuditAsync(Order order, Partner partner, Customer customer, string status, string detail, CancellationToken cancellationToken)
    {
        _db.Interactions.Add(new Interaction
        {
            TenantId = order.TenantId,
            CustomerId = customer.Id,
            PartnerId = partner.Id,
            Type = "order_notification",
            ReferenceId = order.Id,
            Payload = JsonSerializer.Serialize(new
            {
                status,
                detail,
                order.TotalAmount,
                order.Currency
            }),
            CreatedAt = DateTimeOffset.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
    }

    private static string BuildPartnerMessage(Order order, Partner partner, Customer customer, ICollection<OrderItem> items)
    {
        var customerLabel = customer.FullName ?? customer.Email ?? customer.Id.ToString();
        var lines = new List<string>
        {
            $"Venta pagada — orden #{order.Id}",
            $"Cliente: {customerLabel}",
            $"Total: {order.TotalAmount:N0} {order.Currency}",
            $"Items: {items.Count}",
            $"Negocio: {partner.Name}"
        };

        if (!string.IsNullOrWhiteSpace(order.DeliveryProviderName))
        {
            lines.Add($"Delivery: {order.DeliveryProviderName}");
        }

        if (!string.IsNullOrWhiteSpace(order.DeliveryAddress))
        {
            lines.Add($"Dirección despacho: {order.DeliveryAddress}");
        }

        return string.Join(Environment.NewLine, lines);
    }
}
