namespace ComunaClick.Api.Modules.Payouts.Contracts;

public sealed record PartnerPayoutItemResponse(
    Guid Id,
    Guid BatchId,
    Guid PartnerId,
    decimal GrossAmount,
    decimal CommissionAmount,
    decimal SubscriptionDeduction,
    decimal NetAmount,
    string? Currency,
    DateTimeOffset CreatedAt,
    string? BatchStatus
);
