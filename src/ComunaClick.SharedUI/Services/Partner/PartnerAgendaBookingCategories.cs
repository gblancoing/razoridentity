using PartnerBooking = ComunaClick.Shared.Api.Partner.Booking;

namespace ComunaClick.SharedUI.Services.Partner;

public enum AgendaBookingColumn
{
    Pending,
    Completed,
    Missed
}

public static class PartnerAgendaBookingCategories
{
    public static AgendaBookingColumn GetColumn(string? status)
    {
        return Normalize(status) switch
        {
            "completed" => AgendaBookingColumn.Completed,
            "no_show" or "cancelled" or "canceled" => AgendaBookingColumn.Missed,
            _ => AgendaBookingColumn.Pending
        };
    }

    public static bool IsPending(string? status) => GetColumn(status) == AgendaBookingColumn.Pending;

    public static bool IsCompleted(string? status) => GetColumn(status) == AgendaBookingColumn.Completed;

    public static bool IsMissed(string? status) => GetColumn(status) == AgendaBookingColumn.Missed;

    public static (int Pending, int Completed, int Missed) CountByColumn(IEnumerable<PartnerBooking> bookings)
    {
        var pending = 0;
        var completed = 0;
        var missed = 0;
        foreach (var booking in bookings)
        {
            switch (GetColumn(booking.Status))
            {
                case AgendaBookingColumn.Completed:
                    completed++;
                    break;
                case AgendaBookingColumn.Missed:
                    missed++;
                    break;
                default:
                    pending++;
                    break;
            }
        }

        return (pending, completed, missed);
    }

    private static string Normalize(string? status)
        => string.IsNullOrWhiteSpace(status) ? string.Empty : status.Trim().ToLowerInvariant();
}
