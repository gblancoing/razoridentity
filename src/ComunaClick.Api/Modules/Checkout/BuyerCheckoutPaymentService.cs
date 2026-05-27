using ComunaClick.Api.Integrations.Notifications;
using ComunaClick.Api.Modules.Checkout.Contracts;
using ComunaClick.Api.Modules.Marketplace;
using ComunaClick.Api.Modules.Marketplace.Contracts;
using ComunaClick.Api.Persistence;
using ComunaClick.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ComunaClick.Api.Modules.Checkout;

public interface IBuyerCheckoutPaymentService
{
    Task<PublicPartnerPaymentStatusResponse> GetPartnerPaymentStatusAsync(
        Guid partnerId,
        CancellationToken cancellationToken = default);

    Task<PublicMercadoPagoCheckoutResponse> CreateMercadoPagoCheckoutAsync(
        PublicMercadoPagoCheckoutRequest request,
        CancellationToken cancellationToken = default);

    Task<PublicMercadoPagoCheckoutResponse> ResumeMercadoPagoCheckoutAsync(
        PublicMercadoPagoCheckoutRequest request,
        CancellationToken cancellationToken = default);

    Task SendPaymentReminderAsync(
        PublicPaymentReminderRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class BuyerCheckoutPaymentService : IBuyerCheckoutPaymentService
{
    private readonly CoreDbContext _db;
    private readonly MarketplacePaymentService _payments;
    private readonly IOrderNotificationService _orderNotificationService;

    public BuyerCheckoutPaymentService(
        CoreDbContext db,
        MarketplacePaymentService payments,
        IOrderNotificationService orderNotificationService)
    {
        _db = db;
        _payments = payments;
        _orderNotificationService = orderNotificationService;
    }

    public async Task<PublicPartnerPaymentStatusResponse> GetPartnerPaymentStatusAsync(
        Guid partnerId,
        CancellationToken cancellationToken = default)
    {
        var ready = partnerId != Guid.Empty && await _payments.IsSellerCheckoutReadyAsync(partnerId, cancellationToken);
        return new PublicPartnerPaymentStatusResponse(partnerId, ready);
    }

    public async Task<PublicMercadoPagoCheckoutResponse> CreateMercadoPagoCheckoutAsync(
        PublicMercadoPagoCheckoutRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.CustomerId == Guid.Empty)
        {
            return Unavailable("CustomerId is required.");
        }

        var hasOrder = request.OrderId is Guid orderId && orderId != Guid.Empty;
        var hasBooking = request.BookingId is Guid bookingId && bookingId != Guid.Empty;
        if (hasOrder == hasBooking)
        {
            return Unavailable("Provide either OrderId or BookingId.");
        }

        if (hasOrder)
        {
            return await CreateForOrderAsync(request.OrderId!.Value, request.CustomerId, cancellationToken);
        }

        return await CreateForBookingAsync(request.BookingId!.Value, request.CustomerId, cancellationToken);
    }

    public Task<PublicMercadoPagoCheckoutResponse> ResumeMercadoPagoCheckoutAsync(
        PublicMercadoPagoCheckoutRequest request,
        CancellationToken cancellationToken)
        => CreateMercadoPagoCheckoutAsync(request, cancellationToken);

    public async Task SendPaymentReminderAsync(
        PublicPaymentReminderRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.CustomerId == Guid.Empty)
        {
            return;
        }

        var customer = await _db.Customers.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.CustomerId, cancellationToken);
        if (customer is null || string.IsNullOrWhiteSpace(customer.Email))
        {
            return;
        }

        if (request.OrderId is Guid orderId && orderId != Guid.Empty)
        {
            var order = await _db.Orders.AsNoTracking()
                .Include(x => x.Items)
                .FirstOrDefaultAsync(x => x.Id == orderId && x.CustomerId == request.CustomerId, cancellationToken);
            if (order is null || !string.Equals(order.Status, "payment_pending", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var partner = await _db.Partners.AsNoTracking().FirstOrDefaultAsync(x => x.Id == order.PartnerId, cancellationToken);
            if (partner is not null)
            {
                await _orderNotificationService.NotifyBuyerPaymentPendingAsync(order, partner, customer, cancellationToken);
            }

            return;
        }

        if (request.BookingId is Guid bookingId && bookingId != Guid.Empty)
        {
            var booking = await _db.Bookings.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == bookingId && x.CustomerId == request.CustomerId, cancellationToken);
            if (booking is null || !string.Equals(booking.Status, "payment_pending", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var partner = await _db.Partners.AsNoTracking().FirstOrDefaultAsync(x => x.Id == booking.PartnerId, cancellationToken);
            var service = await _db.Services.AsNoTracking().FirstOrDefaultAsync(x => x.Id == booking.ServiceId, cancellationToken);
            if (partner is not null)
            {
                await _orderNotificationService.NotifyBuyerBookingPaymentPendingAsync(
                    booking,
                    partner,
                    customer,
                    service?.Name,
                    cancellationToken);
            }
        }
    }

    private async Task<PublicMercadoPagoCheckoutResponse> CreateForOrderAsync(
        Guid orderId,
        Guid customerId,
        CancellationToken cancellationToken)
    {
        var order = await _db.Orders
            .Include(x => x.Items)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == orderId && x.CustomerId == customerId, cancellationToken);

        if (order is null)
        {
            return Unavailable("Order was not found.");
        }

        if (!await _payments.IsSellerCheckoutReadyAsync(order.PartnerId, cancellationToken))
        {
            return Unavailable("This business has not enabled online payments yet.");
        }

        var customer = await _db.Customers.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == customerId, cancellationToken);

        if (customer is null || string.IsNullOrWhiteSpace(customer.Email))
        {
            return Unavailable("Buyer profile is incomplete.");
        }

        try
        {
            var response = await _payments.CreateBuyerCheckoutProForOrderAsync(
                order,
                customer.Email,
                customer.FullName,
                cancellationToken);

            return new PublicMercadoPagoCheckoutResponse(
                true,
                response.CheckoutUrl,
                response.PaymentId,
                null);
        }
        catch (InvalidOperationException ex)
        {
            return Unavailable(ex.Message);
        }
    }

    private async Task<PublicMercadoPagoCheckoutResponse> CreateForBookingAsync(
        Guid bookingId,
        Guid customerId,
        CancellationToken cancellationToken)
    {
        var booking = await _db.Bookings.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == bookingId && x.CustomerId == customerId, cancellationToken);

        if (booking is null)
        {
            return Unavailable("Booking was not found.");
        }

        if (!await _payments.IsSellerCheckoutReadyAsync(booking.PartnerId, cancellationToken))
        {
            return Unavailable("This business has not enabled online payments yet.");
        }

        var service = await _db.Services.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == booking.ServiceId, cancellationToken);

        var customer = await _db.Customers.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == customerId, cancellationToken);

        if (customer is null || string.IsNullOrWhiteSpace(customer.Email))
        {
            return Unavailable("Buyer profile is incomplete.");
        }

        try
        {
            var response = await _payments.CreateBuyerCheckoutProForBookingAsync(
                booking,
                service?.Name ?? "Reserva ComunaClic",
                customer.Email,
                customer.FullName,
                cancellationToken);

            return new PublicMercadoPagoCheckoutResponse(
                true,
                response.CheckoutUrl,
                response.PaymentId,
                null);
        }
        catch (InvalidOperationException ex)
        {
            return Unavailable(ex.Message);
        }
    }

    private static PublicMercadoPagoCheckoutResponse Unavailable(string? message)
        => new(false, null, null, message);
}
