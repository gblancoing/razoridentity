namespace ComunaClick.Shared.Partner.Payouts;

public sealed record PayoutDto(
    Guid Id,
    string Period,
    decimal Amount,
    string Status,
    DateTimeOffset ScheduledAt
);
