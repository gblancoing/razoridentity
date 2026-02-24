using ComunaClick.Shared.Partner.Bookings;

namespace ComunaClick.Shared.Partner.Interfaces;

public interface IPartnerBookingService
{
    Task<IReadOnlyList<BookingDto>> GetUpcomingBookingsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BookingSlotDto>> GetTodaySlotsAsync(CancellationToken cancellationToken = default);
}
