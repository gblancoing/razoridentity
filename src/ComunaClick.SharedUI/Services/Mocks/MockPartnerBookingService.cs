using ComunaClick.Shared.Partner.Bookings;
using ComunaClick.Shared.Partner.Interfaces;

namespace ComunaClick.SharedUI.Services.Mocks;

public sealed class MockPartnerBookingService : IPartnerBookingService
{
    public Task<IReadOnlyList<BookingDto>> GetUpcomingBookingsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.Now;
        IReadOnlyList<BookingDto> items = new List<BookingDto>
        {
            new(Guid.NewGuid(), "Juan R.", now.AddHours(1), "Corte de cabello", "Confirmada"),
            new(Guid.NewGuid(), "Camila P.", now.AddHours(4), "Yoga básico", "Confirmada"),
            new(Guid.NewGuid(), "Pablo M.", now.AddHours(7), "Masaje relajante", "Pendiente")
        };

        return Task.FromResult(items);
    }

    public Task<IReadOnlyList<BookingSlotDto>> GetTodaySlotsAsync(CancellationToken cancellationToken = default)
    {
        var today = DateTimeOffset.Now.Date;
        IReadOnlyList<BookingSlotDto> items = new List<BookingSlotDto>
        {
            new(Guid.NewGuid(), today.AddHours(9), today.AddHours(10), "Disponible"),
            new(Guid.NewGuid(), today.AddHours(12.5), today.AddHours(13.5), "Reservado"),
            new(Guid.NewGuid(), today.AddHours(16), today.AddHours(17), "Pendiente")
        };

        return Task.FromResult(items);
    }
}
