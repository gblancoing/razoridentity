using ComunaClick.Api.Modules.Orders.Contracts;

namespace ComunaClick.Api.Modules.Bookings.Contracts;

public sealed record GuestBookingCreateRequest(
    Guid TenantId,
    Guid ServiceId,
    Guid? SlotId,
    GuestContactRequest Guest,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    string? Currency = null,
    string? CancellationPolicy = null);

public sealed record GuestBookingCreateResponse(
    Guid BookingId,
    Guid CustomerId,
    string Status,
    decimal Amount,
    string Currency);
