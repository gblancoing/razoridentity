using ComunaClick.Api.Integrations.Notifications;
using ComunaClick.Api.Modules.Bookings.Contracts;
using ComunaClick.Api.Modules.Customers;
using ComunaClick.Api.Persistence;
using ComunaClick.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ComunaClick.Api.Modules.Bookings;

public interface IBookingCheckoutService
{
    Task<(Customer Customer, BookingCheckoutResult Result)> CreateGuestBookingAsync(
        GuestBookingCreateRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record BookingCheckoutResult(
    bool Success,
    Booking? Booking,
    string? ErrorMessage);

public sealed class BookingCheckoutService : IBookingCheckoutService
{
    private readonly CoreDbContext _db;
    private readonly IGuestCustomerService _guestCustomers;
    private readonly IOrderNotificationService _orderNotificationService;

    public BookingCheckoutService(
        CoreDbContext db,
        IGuestCustomerService guestCustomers,
        IOrderNotificationService orderNotificationService)
    {
        _db = db;
        _guestCustomers = guestCustomers;
        _orderNotificationService = orderNotificationService;
    }

    public async Task<(Customer Customer, BookingCheckoutResult Result)> CreateGuestBookingAsync(
        GuestBookingCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = _guestCustomers.ValidateGuestContact(request.Guest);
        if (validation is not null)
        {
            return (null!, Fail(validation));
        }

        if (request.TenantId == Guid.Empty)
        {
            return (null!, Fail("TenantId is required."));
        }

        if (request.EndAt <= request.StartAt)
        {
            return (null!, Fail("EndAt must be after StartAt."));
        }

        var service = await _db.Services.AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == request.ServiceId && x.IsActive, cancellationToken);

        if (service is null || service.TenantId != request.TenantId)
        {
            return (null!, Fail("Selected service is not available for booking."));
        }

        var hasVisiblePartner = await _db.Partners.AsNoTracking()
            .IgnoreQueryFilters()
            .AnyAsync(x => x.Id == service.PartnerId && x.TenantId == request.TenantId && x.IsVisible, cancellationToken);

        if (!hasVisiblePartner)
        {
            return (null!, Fail("Selected partner is not publicly available."));
        }

        var customer = await _guestCustomers.EnsureGuestCustomerAsync(
            request.TenantId,
            request.Guest,
            deliveryAddress: null,
            cancellationToken);

        if (customer is null)
        {
            return (null!, Fail("Could not create buyer profile for this booking."));
        }

        ServiceSlot? slot = null;
        if (request.SlotId.HasValue)
        {
            slot = await _db.ServiceSlots
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x =>
                    x.Id == request.SlotId.Value &&
                    x.TenantId == request.TenantId &&
                    x.PartnerId == service.PartnerId &&
                    x.ServiceId == service.Id,
                    cancellationToken);

            if (slot is null)
            {
                return (null!, Fail("Selected slot does not belong to this service."));
            }

            if (!slot.IsAvailable || slot.Capacity <= 0)
            {
                return (null!, Fail("Selected slot is no longer available."));
            }

            if (slot.StartAt != request.StartAt || slot.EndAt != request.EndAt)
            {
                return (null!, Fail("StartAt/EndAt must match the selected slot."));
            }

            slot.IsAvailable = false;
            slot.Capacity = 0;
        }

        var booking = new Booking
        {
            TenantId = request.TenantId,
            PartnerId = service.PartnerId,
            ServiceId = service.Id,
            SlotId = request.SlotId,
            CustomerId = customer.Id,
            Status = "payment_pending",
            StartAt = request.StartAt,
            EndAt = request.EndAt,
            Amount = service.Price,
            Currency = string.IsNullOrWhiteSpace(request.Currency) ? service.Currency : request.Currency.Trim(),
            CancellationPolicy = string.IsNullOrWhiteSpace(request.CancellationPolicy) ? "{}" : request.CancellationPolicy,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _db.Bookings.Add(booking);
        await _db.SaveChangesAsync(cancellationToken);

        var partner = await _db.Partners.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == service.PartnerId, cancellationToken);
        if (partner is not null)
        {
            await _orderNotificationService.NotifyBuyerBookingAsync(
                booking,
                partner,
                customer,
                service.Name,
                cancellationToken);
        }

        return (customer, new BookingCheckoutResult(true, booking, null));
    }

    private static BookingCheckoutResult Fail(string message)
        => new(false, null, message);
}
