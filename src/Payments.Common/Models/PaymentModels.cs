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

public sealed record PaymentProviderCallbackRequest(
    string Provider,
    string CallbackType,
    IReadOnlyDictionary<string, string?> Query,
    IReadOnlyDictionary<string, string?> Form,
    string? RawBody);

public sealed record PaymentProviderCallbackResult(
    string Provider,
    string? ProviderToken,
    string? ExternalReference,
    string Status,
    string ProviderEventId,
    string? AuthorizationCode,
    string RawPayload,
    string Message);

public sealed record PaymentEvent(
    Guid PaymentId,
    PaymentStatus Status,
    DateTimeOffset OccurredAt,
    string? ProviderEventId = null,
    string? RawPayload = null);
