using ComunaClick.Api.Configuration;
using ComunaClick.Api.Modules.Marketplace;
using ComunaClick.Api.Persistence;
using ComunaClick.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ComunaClick.Api.Modules.Delivery;

public interface IDeliverySettlementService
{
    /// <summary>
    /// Crea (idempotente: 1 por orden) el slip de liquidación del transporte
    /// cuando la orden queda pagada. No hace nada si la orden no es delivery
    /// o no cobró envío.
    /// </summary>
    Task<DeliverySettlement?> CreateForPaidOrderAsync(Order order, Payment? payment, CancellationToken cancellationToken = default);

    /// <summary>
    /// Concilia la porción del fee real de MercadoPago atribuible al envío
    /// (prorrateo deliveryFee/bruto del pago) y recalcula el neto.
    /// </summary>
    Task ReconcileMercadoPagoFeeAsync(Guid orderId, decimal totalMpFee, decimal paymentGross, CancellationToken cancellationToken = default);

    /// <summary>Vincula el transportista asignado al slip (la asignación puede ser posterior al pago).</summary>
    Task AttachCourierAsync(Guid orderId, Guid courierId, CancellationToken cancellationToken = default);

    /// <summary>Marca el slip como liquidado (transferencia efectuada al transportista).</summary>
    Task<(bool Ok, string? Error)> MarkSettledAsync(Guid settlementId, string? notes, CancellationToken cancellationToken = default);

    /// <summary>
    /// El comercio declara que ya pagó al transportista (efectivo o transferencia).
    /// Cambia status a "manual_confirming" y registra el método en Notes.
    /// El transportista debe confirmar el recibo desde su portal para marcar "settled".
    /// </summary>
    Task<(bool Ok, string? Error)> DeclareManualPaymentAsync(Guid settlementId, Guid partnerId, string method, CancellationToken cancellationToken = default);

    /// <summary>
    /// El transportista confirma que recibió el pago manual declarado por el comercio.
    /// Solo válido cuando el status es "manual_confirming".
    /// </summary>
    Task<(bool Ok, string? Error)> ConfirmManualReceiptAsync(Guid settlementId, Guid courierId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resultado del pago comercio → repartidor (external reference cc-delivery-*):
    /// aprobado ⇒ slip liquidado con el fee MP REAL de esa transacción;
    /// rechazado/cancelado ⇒ el slip vuelve a pending para reintentar el link.
    /// </summary>
    Task ApplyCourierPaymentOutcomeAsync(Payment payment, decimal? realMpFee, CancellationToken cancellationToken = default);
}

/// <summary>
/// Split del monto de transporte, replicando el modelo del comercio:
/// neto transportista = bruto envío − fee MercadoPago − comisión ComunaClic.
///
/// Decisión de modelado (documentada): MercadoPago marketplace admite UN solo
/// collector por pago, por lo que el comprador paga UNA vez (collector =
/// comercio) y la liquidación del transportista se registra como un slip
/// SEPARADO e independiente del split del comercio. La comisión ComunaClic se
/// calcula con el mismo FeeCalculator; el fee de MP se concilia con el valor
/// REAL del webhook, prorrateado por la porción del envío dentro del pago.
/// El residuo de redondeo CLP se asigna siempre al neto del transportista.
/// </summary>
public sealed class DeliverySettlementService : IDeliverySettlementService
{
    private readonly CoreDbContext _db;
    private readonly FeeCalculator _feeCalculator;
    private readonly ICourierPayeeService _payeeService;
    private readonly DeliveryPricingOptions _options;

    public DeliverySettlementService(
        CoreDbContext db,
        FeeCalculator feeCalculator,
        ICourierPayeeService payeeService,
        IOptions<DeliveryPricingOptions> options)
    {
        _db = db;
        _feeCalculator = feeCalculator;
        _payeeService = payeeService;
        _options = options.Value;
    }

    public async Task<DeliverySettlement?> CreateForPaidOrderAsync(Order order, Payment? payment, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(order.DeliveryType, DeliveryTypes.Delivery, StringComparison.OrdinalIgnoreCase)
            || order.DeliveryFee <= 0)
        {
            return null;
        }

        var existing = await _db.DeliverySettlements
            .FirstOrDefaultAsync(x => x.OrderId == order.Id, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var gross = FeeCalculator.RoundClp(order.DeliveryFee);
        var ccFee = _feeCalculator.Calculate(
            gross,
            _options.Commission.FixedFeeAmount,
            _options.Commission.PercentageFee);

        // El fee MP puede no estar conciliado aún (llega con el webhook); el
        // neto se recalcula en ReconcileMercadoPagoFeeAsync. Pago manual (sin
        // MP) ⇒ fee MP = 0 definitivo.
        var mpFee = payment is null ? 0m : (decimal?)null;
        var settlement = new DeliverySettlement
        {
            Id = Guid.NewGuid(),
            TenantId = order.TenantId,
            OrderId = order.Id,
            PartnerId = order.PartnerId,
            CourierId = order.CourierId,
            PaymentId = payment?.Id,
            GrossAmount = gross,
            PlatformFeeAmount = ccFee.TotalPlatformFeeAmount,
            MercadoPagoFeeAmount = mpFee,
            NetToCourierAmount = FeeCalculator.RoundClp(gross - ccFee.TotalPlatformFeeAmount - (mpFee ?? 0m)),
            Currency = string.IsNullOrWhiteSpace(order.Currency) ? "CLP" : order.Currency,
            Status = await ResolveInitialStatusAsync(order.CourierId, cancellationToken),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _db.DeliverySettlements.Add(settlement);
        await _db.SaveChangesAsync(cancellationToken);
        return settlement;
    }

    public async Task ReconcileMercadoPagoFeeAsync(Guid orderId, decimal totalMpFee, decimal paymentGross, CancellationToken cancellationToken = default)
    {
        var settlement = await _db.DeliverySettlements
            .FirstOrDefaultAsync(x => x.OrderId == orderId && x.Status != "settled", cancellationToken);
        if (settlement is null || paymentGross <= 0)
        {
            return;
        }

        // Prorrateo: el envío paga la parte del fee MP proporcional a su peso
        // dentro del bruto del pago (productos + envío van en un solo cobro).
        var share = FeeCalculator.RoundClp(totalMpFee * settlement.GrossAmount / paymentGross);
        settlement.MercadoPagoFeeAmount = share;
        // El neto absorbe el residuo del redondeo: bruto = feeMP + feeCC + neto, exacto.
        settlement.NetToCourierAmount = FeeCalculator.RoundClp(
            settlement.GrossAmount - settlement.PlatformFeeAmount - share);
        settlement.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task AttachCourierAsync(Guid orderId, Guid courierId, CancellationToken cancellationToken = default)
    {
        var settlement = await _db.DeliverySettlements
            .FirstOrDefaultAsync(x => x.OrderId == orderId && x.Status != "settled", cancellationToken);
        if (settlement is null)
        {
            return;
        }

        settlement.CourierId = courierId;
        settlement.Status = await ResolveInitialStatusAsync(courierId, cancellationToken);
        settlement.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<(bool Ok, string? Error)> MarkSettledAsync(Guid settlementId, string? notes, CancellationToken cancellationToken = default)
    {
        var settlement = await _db.DeliverySettlements
            .FirstOrDefaultAsync(x => x.Id == settlementId, cancellationToken);
        if (settlement is null)
        {
            return (false, "Settlement was not found.");
        }

        if (string.Equals(settlement.Status, "settled", StringComparison.OrdinalIgnoreCase))
        {
            return (false, "Settlement is already settled.");
        }

        settlement.Status = "settled";
        settlement.SettledAt = DateTimeOffset.UtcNow;
        settlement.Notes = string.IsNullOrWhiteSpace(notes) ? settlement.Notes : notes.Trim();
        settlement.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    public async Task ApplyCourierPaymentOutcomeAsync(Payment payment, decimal? realMpFee, CancellationToken cancellationToken = default)
    {
        var prefix = DeliverySettlementPaymentService.ExternalReferencePrefix;
        if (!payment.ExternalReference.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            || !Guid.TryParse(payment.ExternalReference[prefix.Length..], out var settlementId))
        {
            return;
        }

        var settlement = await _db.DeliverySettlements
            .FirstOrDefaultAsync(x => x.Id == settlementId, cancellationToken);
        if (settlement is null || string.Equals(settlement.Status, "settled", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (string.Equals(payment.Status, "approved", StringComparison.OrdinalIgnoreCase))
        {
            if (realMpFee is not null)
            {
                settlement.MercadoPagoFeeAmount = FeeCalculator.RoundClp(realMpFee.Value);
            }

            // El residuo del redondeo lo absorbe el neto: bruto = feeMP + feeCC + neto exacto.
            settlement.NetToCourierAmount = FeeCalculator.RoundClp(
                settlement.GrossAmount - settlement.PlatformFeeAmount - (settlement.MercadoPagoFeeAmount ?? 0m));
            settlement.PaymentId = payment.Id;
            settlement.Status = "settled";
            settlement.SettledAt = DateTimeOffset.UtcNow;
            settlement.Notes = $"Pagado vía MercadoPago ({payment.MercadoPagoPaymentId ?? payment.Id.ToString("N")}).";
            settlement.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }
        else if (payment.Status is "rejected" or "cancelled" &&
                 string.Equals(settlement.Status, "processing", StringComparison.OrdinalIgnoreCase))
        {
            settlement.Status = "pending";
            settlement.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<(bool Ok, string? Error)> DeclareManualPaymentAsync(Guid settlementId, Guid partnerId, string method, CancellationToken cancellationToken = default)
    {
        var normalized = method.Trim().ToLowerInvariant();
        if (normalized is not ("cash" or "transfer"))
        {
            return (false, "Método de pago inválido. Use 'cash' o 'transfer'.");
        }

        var settlement = await _db.DeliverySettlements
            .FirstOrDefaultAsync(x => x.Id == settlementId && x.PartnerId == partnerId, cancellationToken);
        if (settlement is null)
        {
            return (false, "Liquidación no encontrada.");
        }

        if (settlement.Status is "settled")
        {
            return (false, "Esta liquidación ya fue confirmada.");
        }

        var methodLabel = normalized == "cash" ? "efectivo" : "transferencia bancaria";
        settlement.Status = "manual_confirming";
        settlement.Notes = $"Comercio declaró pago en {methodLabel}. Esperando confirmación del transportista.";
        settlement.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> ConfirmManualReceiptAsync(Guid settlementId, Guid courierId, CancellationToken cancellationToken = default)
    {
        var settlement = await _db.DeliverySettlements
            .FirstOrDefaultAsync(x => x.Id == settlementId && x.CourierId == courierId, cancellationToken);
        if (settlement is null)
        {
            return (false, "Liquidación no encontrada.");
        }

        if (settlement.Status != "manual_confirming")
        {
            return (false, "La liquidación no está en espera de confirmación.");
        }

        settlement.Status = "settled";
        settlement.SettledAt = DateTimeOffset.UtcNow;
        settlement.Notes = (settlement.Notes ?? string.Empty) + " Confirmado por el transportista.";
        settlement.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    /// <summary>
    /// pending = liquidable vía MercadoPago (cuenta conectada); manual = el
    /// transportista no tiene cuenta MP "checkout ready" todavía (pago directo
    /// marcado como tal). Sin transportista asignado queda pending hasta asignar.
    /// </summary>
    private async Task<string> ResolveInitialStatusAsync(Guid? courierId, CancellationToken cancellationToken)
    {
        if (!courierId.HasValue)
        {
            return "pending";
        }

        var status = await _payeeService.GetStatusAsync(courierId.Value, cancellationToken);
        return status.CheckoutReady ? "pending" : "manual";
    }
}
