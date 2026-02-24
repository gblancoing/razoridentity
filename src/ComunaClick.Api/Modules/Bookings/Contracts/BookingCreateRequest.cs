namespace ComunaClick.Api.Modules.Bookings.Contracts;

public sealed record BookingCreateRequest(
    Guid PartnerId,
    Guid ServiceId,
    Guid? SlotId,
    Guid CustomerId,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    decimal Amount,
    string? Currency,
    string? CancellationPolicy);
