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
