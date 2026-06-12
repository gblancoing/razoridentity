namespace ComunaClick.Api.Persistence.Entities;

/// <summary>
/// Slip/comprobante de liquidación del transporte de una orden: desglose
/// auditable de lo que paga el cliente por el envío y cómo se reparte
/// (comisión MercadoPago + comisión ComunaClic + neto al transportista).
/// Es un registro separado del split del comercio: comercio y transportista
/// se liquidan por separado aunque provengan del mismo pedido.
/// </summary>
public sealed class DeliverySettlement
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid OrderId { get; set; }
    public Guid PartnerId { get; set; }

    /// <summary>Transportista asignado (puede asignarse después del pago; se actualiza al asignar).</summary>
    public Guid? CourierId { get; set; }

    /// <summary>Pago MercadoPago del que proviene el monto (null en pagos manuales/efectivo).</summary>
    public Guid? PaymentId { get; set; }

    /// <summary>Bruto del envío cobrado al cliente (DeliveryFee de la orden).</summary>
    public decimal GrossAmount { get; set; }

    /// <summary>Comisión ComunaClic del transporte (fijo + %, FeeCalculator).</summary>
    public decimal PlatformFeeAmount { get; set; }

    /// <summary>
    /// Porción del fee real de MercadoPago atribuible al envío (prorrateada por
    /// deliveryFee/bruto del pago). Null hasta que el webhook concilia el fee.
    /// </summary>
    public decimal? MercadoPagoFeeAmount { get; set; }

    /// <summary>
    /// Neto a depositar al transportista = bruto − feeMP − feeComunaClic.
    /// El residuo de redondeo CLP se asigna SIEMPRE a este neto (no se pierden pesos).
    /// </summary>
    public decimal NetToCourierAmount { get; set; }

    public string Currency { get; set; } = "CLP";

    /// <summary>pending (a liquidar vía MP) | manual (sin cuenta MP: pago directo) | settled.</summary>
    public string Status { get; set; } = "pending";

    public DateTimeOffset? SettledAt { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
