using System.Text.Json;
using ComunaClick.Api.Modules.Marketplace;
using ComunaClick.Api.Persistence;
using ComunaClick.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ComunaClick.Api.Modules.Delivery;

public sealed record DeliverySettlementPaymentLink(string InitPoint, string Status);

public interface IDeliverySettlementPaymentService
{
    /// <summary>
    /// Genera (idempotente) el link de pago MercadoPago con el que el comercio
    /// paga el envío al transportista (collector = cuenta MP del repartidor,
    /// application_fee = comisión ComunaClic del transporte).
    /// </summary>
    Task<(DeliverySettlementPaymentLink? Link, string? Error)> CreatePaymentLinkAsync(
        Guid settlementId,
        Guid partnerId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Pago comercio → repartidor: el cliente paga UNA sola vez al comercio
/// (productos + envío); tras la entrega, el comercio paga el BRUTO del envío
/// al transportista vía Checkout Pro. De ese pago MercadoPago descuenta su fee
/// real y la comisión ComunaClic viaja como application_fee, por lo que el
/// neto cae directo en la cuenta MP del repartidor y el slip se liquida solo
/// con el webhook (reemplazando el prorrateo estimado por el fee real).
/// </summary>
public sealed class DeliverySettlementPaymentService : IDeliverySettlementPaymentService
{
    public const string ExternalReferencePrefix = "cc-delivery-";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly CoreDbContext _db;
    private readonly MercadoPagoOAuthService _oauthService;
    private readonly MercadoPagoMarketplaceClient _mpClient;
    private readonly ICourierPayeeService _payeeService;
    private readonly MercadoPagoMarketplaceOptions _options;

    public DeliverySettlementPaymentService(
        CoreDbContext db,
        MercadoPagoOAuthService oauthService,
        MercadoPagoMarketplaceClient mpClient,
        ICourierPayeeService payeeService,
        IOptions<MercadoPagoMarketplaceOptions> options)
    {
        _db = db;
        _oauthService = oauthService;
        _mpClient = mpClient;
        _payeeService = payeeService;
        _options = options.Value;
    }

    public async Task<(DeliverySettlementPaymentLink? Link, string? Error)> CreatePaymentLinkAsync(
        Guid settlementId,
        Guid partnerId,
        CancellationToken cancellationToken = default)
    {
        var settlement = await _db.DeliverySettlements
            .FirstOrDefaultAsync(x => x.Id == settlementId && x.PartnerId == partnerId, cancellationToken);
        if (settlement is null)
        {
            return (null, "La liquidación no existe.");
        }

        if (string.Equals(settlement.Status, "settled", StringComparison.OrdinalIgnoreCase))
        {
            return (null, "Este envío ya fue pagado al repartidor.");
        }

        if (settlement.CourierId is null)
        {
            return (null, "El pedido no tiene repartidor asignado.");
        }

        var order = await _db.Orders.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == settlement.OrderId, cancellationToken);
        if (order is null)
        {
            return (null, "El pedido asociado no existe.");
        }

        if (!string.Equals(order.DeliveryStatus, DeliveryStatuses.Delivered, StringComparison.OrdinalIgnoreCase))
        {
            return (null, "El envío aún no está entregado: el pago al repartidor se habilita tras la entrega.");
        }

        // Se revalida la cuenta en vivo: un slip "manual" se vuelve pagable si
        // el repartidor vinculó su cuenta MP después.
        var payee = await _payeeService.GetStatusAsync(settlement.CourierId.Value, cancellationToken);
        if (!payee.CheckoutReady)
        {
            return (null, "El repartidor aún no vincula su cuenta MercadoPago.");
        }

        var externalReference = $"{ExternalReferencePrefix}{settlement.Id:N}";

        // Idempotencia: si ya hay un pago pendiente para este slip se devuelve
        // el mismo link; si fue rechazado/cancelado se reutiliza la MISMA fila
        // Payment (mismo external reference) con una preference nueva.
        Payment? payment = null;
        if (settlement.PaymentId.HasValue)
        {
            payment = await _db.Payments
                .FirstOrDefaultAsync(x => x.Id == settlement.PaymentId.Value && x.ExternalReference == externalReference, cancellationToken);

            if (payment is not null && string.Equals(payment.Status, "approved", StringComparison.OrdinalIgnoreCase))
            {
                return (null, "Este envío ya fue pagado al repartidor.");
            }

            if (payment is not null
                && string.Equals(payment.Status, "pending", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(payment.RawResponseJson))
            {
                var existing = TryReadInitPoint(payment.RawResponseJson);
                if (!string.IsNullOrWhiteSpace(existing))
                {
                    return (new DeliverySettlementPaymentLink(existing!, settlement.Status), null);
                }
            }
        }

        var partner = await _db.Partners.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == settlement.PartnerId, cancellationToken);

        // Collector = repartidor: su fila Seller comparte Id con el courier.
        var accessToken = await _oauthService.GetSellerAccessTokenAsync(settlement.CourierId.Value, cancellationToken);

        if (payment is null)
        {
            payment = new Payment
            {
                TenantId = settlement.TenantId,
                SellerId = settlement.CourierId.Value,
                OrderId = null,
                Provider = "mercadopago",
                ExternalReference = externalReference,
                Amount = settlement.GrossAmount,
                TransactionAmount = settlement.GrossAmount,
                Currency = string.IsNullOrWhiteSpace(settlement.Currency) ? _options.Currency : settlement.Currency,
                Status = "pending",
                IdempotencyKey = $"cc-delivery-pay:{settlement.Id:N}",
                CorrelationId = Guid.NewGuid().ToString("N"),
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            _db.Payments.Add(payment);
            await _db.SaveChangesAsync(cancellationToken);

            _db.PaymentFees.Add(new PaymentFee
            {
                PaymentId = payment.Id,
                PlatformFeeAmount = settlement.PlatformFeeAmount,
                NetToSellerAmount = FeeCalculator.RoundClp(settlement.GrossAmount - settlement.PlatformFeeAmount),
                CreatedAt = DateTimeOffset.UtcNow
            });
        }
        else
        {
            // Reintento tras rechazo: misma fila, estado de vuelta a pending.
            payment.Status = "pending";
            payment.StatusDetail = null;
            payment.UpdatedAt = DateTimeOffset.UtcNow;
        }

        var baseUrl = _options.AppBaseUrl.TrimEnd('/');
        var backUrl = $"{baseUrl}/partner/orders?orderId={settlement.OrderId}";
        var webhookBase = (string.IsNullOrWhiteSpace(_options.WebhookBaseUrl) ? _options.AppBaseUrl : _options.WebhookBaseUrl).TrimEnd('/');

        var preference = await _mpClient.CreateCheckoutProPreferenceAsync(
            accessToken,
            new MercadoPagoPreferenceRequest(
                externalReference,
                settlement.PlatformFeeAmount,
                new MercadoPagoBackUrls(backUrl, backUrl, backUrl),
                new[]
                {
                    new MercadoPagoPreferenceItem(
                        settlement.Id.ToString("N"),
                        $"Envío pedido #{settlement.OrderId.ToString()[..8]}",
                        1,
                        payment.Currency,
                        settlement.GrossAmount)
                },
                new MercadoPagoPreferencePayer(
                    partner?.Email ?? $"partner-{settlement.PartnerId:N}@comunaclic.cl",
                    partner?.Name),
                $"{webhookBase}/api/webhooks/mercadopago"),
            cancellationToken);

        payment.ProviderToken = preference.Id;
        payment.RawResponseJson = JsonSerializer.Serialize(preference, JsonOptions);
        payment.StatusDetail = "checkout_pro_preference_created";

        _db.PaymentStatusHistory.Add(new PaymentStatusHistory
        {
            PaymentId = payment.Id,
            PreviousStatus = null,
            NewStatus = payment.Status,
            Detail = payment.StatusDetail,
            RawPayloadJson = payment.RawResponseJson,
            CreatedAt = DateTimeOffset.UtcNow
        });

        settlement.PaymentId = payment.Id;
        settlement.Status = "processing";
        settlement.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        var initPoint = preference.InitPoint ?? preference.SandboxInitPoint;
        return string.IsNullOrWhiteSpace(initPoint)
            ? (null, "MercadoPago no devolvió el link de pago. Intenta de nuevo.")
            : (new DeliverySettlementPaymentLink(initPoint!, settlement.Status), null);
    }

    private static string? TryReadInitPoint(string rawJson)
    {
        try
        {
            var preference = JsonSerializer.Deserialize<MercadoPagoPreferenceResponse>(rawJson, JsonOptions);
            return preference?.InitPoint ?? preference?.SandboxInitPoint;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
