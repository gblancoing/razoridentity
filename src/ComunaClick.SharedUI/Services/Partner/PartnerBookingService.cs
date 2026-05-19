using ComunaClick.Shared.Partner.Bookings;
using ComunaClick.Shared.Partner.Interfaces;
using ComunaClick.SharedUI.Services;

namespace ComunaClick.SharedUI.Services.Partner;

public sealed class PartnerBookingService : PartnerServiceBase, IPartnerBookingService
{
    public PartnerBookingService(ComunaClick.Shared.Api.Partner.PartnerApiClient partnerApi, AuthStateService authState)
        : base(partnerApi, authState)
    {
    }

    public async Task<IReadOnlyList<BookingDto>> GetUpcomingBookingsAsync(CancellationToken cancellationToken = default)
    {
        var partnerId = await ResolvePartnerIdAsync(cancellationToken);
        if (!partnerId.HasValue)
        {
            return Array.Empty<BookingDto>();
        }

        var bookings = await PartnerApi.GetPartnerBookingsAsync(partnerId.Value, cancellationToken) ?? [];
        return bookings
            .OrderBy(x => x.StartAt)
            .Take(12)
            .Select(x => new BookingDto(
                x.Id,
                "Cliente ComunaClic",
                x.StartAt,
                $"Servicio {x.ServiceId.ToString()[..8]}",
                x.Status ?? "pending"))
            .ToList();
    }

    public async Task<IReadOnlyList<BookingSlotDto>> GetTodaySlotsAsync(CancellationToken cancellationToken = default)
    {
        var partnerId = await ResolvePartnerIdAsync(cancellationToken);
        if (!partnerId.HasValue)
        {
            return Array.Empty<BookingSlotDto>();
        }

        var bookings = await PartnerApi.GetPartnerBookingsAsync(partnerId.Value, cancellationToken) ?? [];
        var today = DateTimeOffset.Now.Date;
        var tomorrow = today.AddDays(1);

        return bookings
            .Where(x => x.StartAt.ToLocalTime() >= today && x.StartAt.ToLocalTime() < tomorrow)
            .OrderBy(x => x.StartAt)
            .Select(x => new BookingSlotDto(
                x.Id,
                x.StartAt,
                x.EndAt,
                x.Status ?? "pending"))
            .ToList();
    }
}
