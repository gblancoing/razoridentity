namespace Payments.Gateway.Api.Contracts.Admin;

/// <summary>Fila de conciliación PaymentIntent + ProviderEvent + Charge.</summary>
public sealed record AdminReconciliationRowDto(
    string Kind,
    Guid? IntentId,
    string? ExternalReference,
    string? Provider,
    string? Status,
    decimal? Amount,
    string? Currency,
    string Detail,
    DateTimeOffset OccurredAt);

public sealed record AdminAlertsDto(
    int StuckPendingCount,
    int FlaggedCount,
    int FailedLast7DaysCount,
    int ChargesWithoutAuthCount,
    int CoreNotifyFailingCount);

public sealed record AdminReviewRequest(string Status, string? Note);
