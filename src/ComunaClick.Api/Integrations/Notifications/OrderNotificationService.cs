using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Mail;
using System.Text.Json;
using ComunaClick.Api.Persistence;
using ComunaClick.Api.Persistence.Entities;
using Microsoft.Extensions.Options;

namespace ComunaClick.Api.Integrations.Notifications;

public sealed class OrderNotificationService : IOrderNotificationService
{
    private readonly OrderNotificationOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly CoreDbContext _db;
    private readonly ILogger<OrderNotificationService> _logger;

    public OrderNotificationService(
        IOptions<OrderNotificationOptions> options,
        IHttpClientFactory httpClientFactory,
        CoreDbContext db,
        ILogger<OrderNotificationService> logger)
    {
        _options = options.Value;
        _httpClientFactory = httpClientFactory;
        _db = db;
        _logger = logger;
    }

    public async Task NotifyPartnerAsync(Order order, Partner partner, Customer customer, IReadOnlyList<OrderItem> items, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            await SaveNotificationAuditAsync(order, partner, customer, "disabled", "notifications_disabled", cancellationToken);
            return;
        }

        var message = BuildMessage(order, partner, customer, items);

        var outcomes = new List<string>();
        if (_options.EnableEmail)
        {
            outcomes.Add(await TrySendEmailAsync(partner, message, cancellationToken));
        }

        if (_options.EnableWhatsAppWebhook)
        {
            outcomes.Add(await TrySendWhatsAppAsync(partner, order, customer, message, cancellationToken));
        }

        if (outcomes.Count == 0)
        {
            outcomes.Add("no_channel_enabled");
        }

        await SaveNotificationAuditAsync(order, partner, customer, "processed", string.Join(",", outcomes), cancellationToken);
    }

    private async Task<string> TrySendEmailAsync(Partner partner, string message, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(partner.Email))
        {
            return "email_missing_partner_email";
        }

        if (string.IsNullOrWhiteSpace(_options.Smtp.Host) || string.IsNullOrWhiteSpace(_options.Smtp.From))
        {
            return "email_missing_smtp_config";
        }

        try
        {
            using var smtp = new SmtpClient(_options.Smtp.Host, _options.Smtp.Port)
            {
                EnableSsl = _options.Smtp.EnableSsl
            };

            if (!string.IsNullOrWhiteSpace(_options.Smtp.Username))
            {
                smtp.Credentials = new NetworkCredential(_options.Smtp.Username, _options.Smtp.Password);
            }

            using var mail = new MailMessage(_options.Smtp.From, partner.Email.Trim())
            {
                Subject = "Nueva orden de compra - ComunaClic",
                Body = message
            };

            await smtp.SendMailAsync(mail, cancellationToken);
            return "email_sent";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to send order email notification for partner {PartnerId}.", partner.Id);
            return "email_error";
        }
    }

    private async Task<string> TrySendWhatsAppAsync(Partner partner, Order order, Customer customer, string message, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.WhatsAppWebhookUrl))
        {
            return "wa_missing_webhook";
        }

        if (string.IsNullOrWhiteSpace(partner.Phone))
        {
            return "wa_missing_partner_phone";
        }

        try
        {
            var client = _httpClientFactory.CreateClient(nameof(OrderNotificationService));
            using var request = new HttpRequestMessage(HttpMethod.Post, _options.WhatsAppWebhookUrl)
            {
                Content = JsonContent.Create(new
                {
                    channel = "whatsapp",
                    orderId = order.Id,
                    partnerId = partner.Id,
                    to = partner.Phone,
                    customer = customer.FullName ?? customer.Email ?? customer.Id.ToString(),
                    message
                })
            };

            if (!string.IsNullOrWhiteSpace(_options.WhatsAppApiKey))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.WhatsAppApiKey);
            }

            using var response = await client.SendAsync(request, cancellationToken);
            return response.IsSuccessStatusCode ? "wa_sent" : $"wa_http_{(int)response.StatusCode}";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to send WhatsApp notification for partner {PartnerId}.", partner.Id);
            return "wa_error";
        }
    }

    private async Task SaveNotificationAuditAsync(Order order, Partner partner, Customer customer, string status, string detail, CancellationToken cancellationToken)
    {
        var interaction = new Interaction
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
        };

        _db.Interactions.Add(interaction);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static string BuildMessage(Order order, Partner partner, Customer customer, IReadOnlyList<OrderItem> items)
    {
        var customerLabel = customer.FullName ?? customer.Email ?? customer.Id.ToString();
        var lines = new List<string>
        {
            $"Nueva orden #{order.Id}",
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
