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
    Stripe,
    MercadoPago,
    PayPal
}

public sealed record PaymentEvent(
    Guid PaymentId,
    PaymentStatus Status,
    DateTimeOffset OccurredAt,
    string? ProviderEventId = null,
    string? RawPayload = null);
