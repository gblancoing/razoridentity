namespace ComunaClick.Shared.Partner.Bookings;

public sealed record BookingSlotDto(
    Guid Id,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    string Status
);
