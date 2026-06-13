namespace ComunaClick.Api.Modules.Checkout.Contracts;

public sealed record PublicPartnerPaymentStatusResponse(
    Guid PartnerId,
    bool MercadoPagoReady);

public sealed record PublicMercadoPagoCheckoutRequest(
    Guid CustomerId,
    Guid? OrderId,
    Guid? BookingId);

public sealed record PublicMercadoPagoCheckoutResponse(
    bool Available,
    string? CheckoutUrl,
    Guid? PaymentId,
    string? Message);

public sealed record PublicPaymentReminderRequest(
    Guid CustomerId,
    Guid? OrderId,
    Guid? BookingId);

/// <summary>
/// Cotización de despacho previa al checkout. <c>Fee</c> usa exactamente el
/// mismo cálculo que aplicará la creación de la orden (paridad cotización ==
/// cobro); el detalle distancia/perfil es informativo.
/// </summary>
public sealed record PublicDeliveryQuoteResponse(
    bool Available,
    bool FeeApplies,
    decimal Fee,
    string Currency,
    double? DistanceKm,
    string? ProfileName,
    Guid? DeliveryProviderId,
    string? DeliveryProviderName,
    bool OutOfRange,
    double? MaxDistanceKm,
    string? Message);
