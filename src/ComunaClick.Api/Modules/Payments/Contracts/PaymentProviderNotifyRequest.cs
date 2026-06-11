namespace ComunaClick.Api.Modules.Payments.Contracts;

public sealed record PaymentProviderNotifyRequest(
    Guid? PaymentId,
    string? ExternalReference,
    string ProviderEventId,
    string Status,
    string? Payload,
    decimal? Amount = null);
