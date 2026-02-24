namespace ComunaClick.Shared.Partner.Bookings;

public sealed record BookingDto(
    Guid Id,
    string CustomerName,
    DateTimeOffset StartTime,
    string ServiceName,
    string Status
);
