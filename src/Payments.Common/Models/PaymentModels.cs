namespace Payments.Common.Models;

public enum PaymentStatus
{
    Pending,
    Authorized,
    Captured,
    Failed,
    Refunded,
    Canceled
}

public enum PaymentProvider
{
    Unknown,
    Transbank,
    Khipu,
    Stripe,
    MercadoPago,
    PayPal
}

public sealed record PaymentProviderCreateRequest(
    string ExternalReference,
    decimal Amount,
    string Currency,
    string ReturnUrl,
    string? Subject = null,
    string? BuyerEmail = null);

public sealed record PaymentProviderCreateResponse(
    string Provider,
    string? ProviderToken,
    string? RedirectUrl,
    string Status,
    string RawResponse);

public sealed record PaymentEvent(
    Guid PaymentId,
    PaymentStatus Status,
    DateTimeOffset OccurredAt,
    string? ProviderEventId = null,
    string? RawPayload = null);
