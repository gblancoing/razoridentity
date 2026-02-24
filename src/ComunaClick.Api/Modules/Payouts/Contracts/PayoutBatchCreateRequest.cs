namespace ComunaClick.Api.Modules.Payouts.Contracts;

public sealed record PayoutBatchCreateRequest(DateOnly PeriodStart, DateOnly PeriodEnd);
