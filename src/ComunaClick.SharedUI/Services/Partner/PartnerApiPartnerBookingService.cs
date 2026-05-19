using System.Net.Http;
using ComunaClick.Shared.Api.Partner;
using ComunaClick.Shared.Partner.Bookings;
using ComunaClick.Shared.Partner.Interfaces;

namespace ComunaClick.SharedUI.Services.Partner;

public sealed class PartnerApiPartnerBookingService : IPartnerBookingService
{
    private readonly PartnerApiClient _partnerApi;
    private readonly AuthStateService _authState;

    public PartnerApiPartnerBookingService(PartnerApiClient partnerApi, AuthStateService authState)
    {
        _partnerApi = partnerApi;
        _authState = authState;
    }

    public async Task<IReadOnlyList<BookingDto>> GetUpcomingBookingsAsync(CancellationToken cancellationToken = default)
    {
        var partnerId = await PartnerApiServiceHelper.ResolvePartnerIdAsync(_authState, _partnerApi, cancellationToken);
        if (!partnerId.HasValue)
            return Array.Empty<BookingDto>();

        try
        {
            var bookings = await _partnerApi.GetPartnerBookingsAsync(partnerId.Value, cancellationToken) ?? Array.Empty<Booking>();
            var services = await _partnerApi.GetPartnerServicesAsync(partnerId.Value, cancellationToken) ?? Array.Empty<Service>();
            var serviceNames = services.ToDictionary(s => s.Id, s => s.Name ?? "Servicio");

            var now = DateTimeOffset.UtcNow;
            return bookings
                .Where(b => b.EndAt >= now)
                .OrderBy(b => b.StartAt)
                .Select(b => new BookingDto(
                    b.Id,
                    $"Cliente {b.CustomerId.ToString()[..8]}…",
                    b.StartAt,
                    serviceNames.GetValueOrDefault(b.ServiceId, "Servicio"),
                    b.Status ?? "—"))
                .ToList();
        }
        catch (HttpRequestException)
        {
            return Array.Empty<BookingDto>();
        }
    }

    public async Task<IReadOnlyList<BookingSlotDto>> GetTodaySlotsAsync(CancellationToken cancellationToken = default)
    {
        var partnerId = await PartnerApiServiceHelper.ResolvePartnerIdAsync(_authState, _partnerApi, cancellationToken);
        if (!partnerId.HasValue)
            return Array.Empty<BookingSlotDto>();

        try
        {
            var bookings = await _partnerApi.GetPartnerBookingsAsync(partnerId.Value, cancellationToken) ?? Array.Empty<Booking>();
            var startDay = DateTimeOffset.Now.Date;
            var endDay = startDay.AddDays(1);

            return bookings
                .Where(b => b.StartAt >= startDay && b.StartAt < endDay)
                .OrderBy(b => b.StartAt)
                .Select(b => new BookingSlotDto(
                    b.Id,
                    b.StartAt,
                    b.EndAt,
                    MapSlotStatus(b.Status)))
                .ToList();
        }
        catch (HttpRequestException)
        {
            return Array.Empty<BookingSlotDto>();
        }
    }

    private static string MapSlotStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return "Pendiente";

        return status.ToLowerInvariant() switch
        {
            "confirmed" or "confirmada" => "Reservado",
            "canceled" or "cancelled" or "cancelada" => "Cancelado",
            "completed" or "completada" => "Completado",
            "payment_pending" => "Pendiente",
            _ => status
        };
    }
}
