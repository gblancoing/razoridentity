using System.Text.Json;
using ComunaClick.Api.Modules.Catalog;
using ComunaClick.Api.Persistence;
using ComunaClick.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ComunaClick.Api.Modules.Marketplace;

public sealed class MercadoPagoWebhookService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly CoreDbContext _db;
    private readonly MercadoPagoMarketplaceClient _client;
    private readonly MercadoPagoOAuthService _oauthService;
    private readonly MarketplaceAuditService _auditService;
    private readonly MarketplaceMetricsService _metrics;
    private readonly IProductInventoryService _inventoryService;

    public MercadoPagoWebhookService(
        CoreDbContext db,
        MercadoPagoMarketplaceClient client,
        MercadoPagoOAuthService oauthService,
        MarketplaceAuditService auditService,
        MarketplaceMetricsService metrics,
        IProductInventoryService inventoryService)
    {
        _db = db;
        _client = client;
        _oauthService = oauthService;
        _auditService = auditService;
        _metrics = metrics;
        _inventoryService = inventoryService;
    }

    public async Task<WebhookEvent> HandleAsync(HttpRequest request, string payloadJson, CancellationToken cancellationToken)
    {
        using var payload = JsonDocument.Parse(string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson);
        var topic = GetString(payload.RootElement, "type")
            ?? GetString(payload.RootElement, "topic")
            ?? "payment";
        var action = GetString(payload.RootElement, "action");
        var resourceId = GetString(payload.RootElement, "data", "id")
            ?? GetString(payload.RootElement, "id");

        var signatureValid = !string.IsNullOrWhiteSpace(resourceId) && _client.ValidateWebhookSignature(request, resourceId);
        var webhookEvent = new WebhookEvent
        {
            Topic = topic,
            Action = action,
            ResourceId = resourceId,
            PayloadJson = payloadJson,
            SignatureValid = signatureValid,
            Processed = false,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var duplicate = await _db.WebhookEvents
            .IgnoreQueryFilters()
            .AnyAsync(x => x.Topic == topic && x.Action == action && x.ResourceId == resourceId && x.Processed, cancellationToken);
        if (duplicate)
        {
            webhookEvent.Processed = true;
            webhookEvent.ProcessedAt = DateTimeOffset.UtcNow;
            _db.WebhookEvents.Add(webhookEvent);
            await _db.SaveChangesAsync(cancellationToken);
            return webhookEvent;
        }

        _db.WebhookEvents.Add(webhookEvent);
        await _db.SaveChangesAsync(cancellationToken);

        if (!signatureValid)
        {
            webhookEvent.ErrorMessage = "Invalid Mercado Pago webhook signature.";
            webhookEvent.Processed = false;
            webhookEvent.ProcessedAt = DateTimeOffset.UtcNow;
            _metrics.Increment("webhook_errors");
            await _db.SaveChangesAsync(cancellationToken);
            return webhookEvent;
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(resourceId))
            {
                await SyncPaymentAsync(resourceId, webhookEvent, cancellationToken);
            }

            webhookEvent.Processed = true;
            webhookEvent.ProcessedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            webhookEvent.ErrorMessage = ex.Message;
            webhookEvent.Processed = false;
            webhookEvent.ProcessedAt = DateTimeOffset.UtcNow;
            _metrics.Increment("webhook_errors");
            await _db.SaveChangesAsync(cancellationToken);
        }

        return webhookEvent;
    }

    public async Task SyncPaymentAsync(Guid paymentId, CancellationToken cancellationToken)
    {
        var payment = await _db.Payments.FirstOrDefaultAsync(x => x.Id == paymentId, cancellationToken)
            ?? throw new InvalidOperationException("Payment not found.");

        if (string.IsNullOrWhiteSpace(payment.MercadoPagoPaymentId) || !payment.SellerId.HasValue)
        {
            return;
        }

        var webhookEvent = new WebhookEvent
        {
            Topic = "manual_sync",
            Action = "manual_sync",
            ResourceId = payment.MercadoPagoPaymentId,
            PayloadJson = "{}",
            SignatureValid = true,
            Processed = false,
            CreatedAt = DateTimeOffset.UtcNow
        };
        _db.WebhookEvents.Add(webhookEvent);
        await _db.SaveChangesAsync(cancellationToken);
        await SyncPaymentAsync(payment.MercadoPagoPaymentId, webhookEvent, cancellationToken);
        webhookEvent.Processed = true;
        webhookEvent.ProcessedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task SyncPaymentAsync(string mercadopagoPaymentId, WebhookEvent webhookEvent, CancellationToken cancellationToken)
    {
        var payment = await _db.Payments.FirstOrDefaultAsync(x => x.MercadoPagoPaymentId == mercadopagoPaymentId, cancellationToken)
            ?? await _db.Payments.FirstOrDefaultAsync(x => x.ProviderToken == mercadopagoPaymentId, cancellationToken);
        if (payment is null || !payment.SellerId.HasValue)
        {
            return;
        }

        var accessToken = await _oauthService.GetSellerAccessTokenAsync(payment.SellerId.Value, cancellationToken);
        var details = await _client.GetPaymentAsync(accessToken, mercadopagoPaymentId, cancellationToken);
        if (details is null)
        {
            return;
        }

        var previousStatus = payment.Status;
        payment.Status = NormalizeMercadoPagoStatus(details.Status);
        payment.StatusDetail = details.StatusDetail;
        payment.PaymentMethod = details.PaymentMethodId ?? payment.PaymentMethod;
        payment.PaidAmount = details.TransactionAmount;
        payment.DateApproved = details.DateApproved;
        payment.RawResponseJson = JsonSerializer.Serialize(details, JsonOptions);
        payment.LastEventId = $"{webhookEvent.Topic}:{webhookEvent.ResourceId}:{webhookEvent.Action}";
        payment.UpdatedAt = DateTimeOffset.UtcNow;

        var paymentFee = await _db.PaymentFees.FirstOrDefaultAsync(x => x.PaymentId == payment.Id, cancellationToken);
        if (paymentFee is not null && details.FeeDetails is not null)
        {
            paymentFee.MercadoPagoFeeAmount = details.FeeDetails
                .Where(x => x.Amount.HasValue)
                .Sum(x => x.Amount!.Value);
        }

        _db.PaymentStatusHistory.Add(new PaymentStatusHistory
        {
            PaymentId = payment.Id,
            PreviousStatus = previousStatus,
            NewStatus = payment.Status,
            Detail = details.StatusDetail,
            RawPayloadJson = payment.RawResponseJson,
            CreatedAt = DateTimeOffset.UtcNow
        });

        if (!string.Equals(previousStatus, payment.Status, StringComparison.OrdinalIgnoreCase))
        {
            _metrics.Increment(payment.Status switch
            {
                "approved" => "payments_approved",
                "rejected" => "payments_rejected",
                _ => "payments_status_changed"
            });
        }

        await _auditService.WriteAsync(
            "mercadopago.webhook",
            "marketplace.payment.synced",
            nameof(Payment),
            payment.Id.ToString(),
            new
            {
                payment.MercadoPagoPaymentId,
                previousStatus,
                newStatus = payment.Status,
                webhookEvent.Topic,
                webhookEvent.Action
            },
            cancellationToken);

        if (payment.OrderId.HasValue && OrderInventoryFulfillment.IsPaidStatus(payment.Status))
        {
            var order = await _db.Orders.FirstOrDefaultAsync(x => x.Id == payment.OrderId.Value, cancellationToken);
            if (order is not null)
            {
                var orderPreviousStatus = order.Status;
                if (!OrderInventoryFulfillment.IsPaidStatus(orderPreviousStatus))
                {
                    order.Status = "paid";
                    order.UpdatedAt = DateTimeOffset.UtcNow;
                    await _db.SaveChangesAsync(cancellationToken);
                }

                await OrderInventoryFulfillment.TryFulfillPaidOrderAsync(
                    _db,
                    _inventoryService,
                    payment.OrderId.Value,
                    orderPreviousStatus,
                    "paid",
                    cancellationToken);
            }
        }
        else if (OrderInventoryFulfillment.IsPaidStatus(payment.Status) &&
                 payment.ExternalReference.StartsWith(MarketplacePaymentService.BookingExternalReferencePrefix, StringComparison.OrdinalIgnoreCase))
        {
            var bookingIdRaw = payment.ExternalReference[MarketplacePaymentService.BookingExternalReferencePrefix.Length..];
            if (Guid.TryParse(bookingIdRaw, out var bookingId))
            {
                var booking = await _db.Bookings.FirstOrDefaultAsync(x => x.Id == bookingId, cancellationToken);
                if (booking is not null && !string.Equals(booking.Status, "confirmed", StringComparison.OrdinalIgnoreCase))
                {
                    booking.Status = "confirmed";
                    booking.UpdatedAt = DateTimeOffset.UtcNow;
                    await _db.SaveChangesAsync(cancellationToken);
                }
            }
        }
    }

    private static string NormalizeMercadoPagoStatus(string? status)
        => status?.Trim().ToLowerInvariant() switch
        {
            "approved" => "approved",
            "authorized" => "authorized",
            "rejected" => "rejected",
            "cancelled" or "canceled" => "cancelled",
            "refunded" => "refunded",
            "charged_back" => "charged_back",
            "in_process" => "in_process",
            _ => "pending"
        };

    private static string? GetString(JsonElement element, params string[] path)
    {
        var current = element;
        foreach (var segment in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
            {
                return null;
            }
        }

        return current.ValueKind == JsonValueKind.String ? current.GetString() : current.ToString();
    }
}
