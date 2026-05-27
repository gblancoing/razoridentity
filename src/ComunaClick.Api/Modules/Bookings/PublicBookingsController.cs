using ComunaClick.Api.Modules.Bookings.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ComunaClick.Api.Modules.Bookings;

[ApiController]
[Route("v1/public/bookings")]
public sealed class PublicBookingsController : ControllerBase
{
    private readonly IBookingCheckoutService _bookingCheckoutService;

    public PublicBookingsController(IBookingCheckoutService bookingCheckoutService)
    {
        _bookingCheckoutService = bookingCheckoutService;
    }

    [AllowAnonymous]
    [EnableRateLimiting("public-write")]
    [HttpPost("guest")]
    public async Task<ActionResult<GuestBookingCreateResponse>> CreateGuest(
        GuestBookingCreateRequest request,
        CancellationToken cancellationToken)
    {
        var (customer, result) = await _bookingCheckoutService.CreateGuestBookingAsync(request, cancellationToken);
        if (!result.Success || result.Booking is null || customer is null)
        {
            return BadRequest(new { message = result.ErrorMessage ?? "Could not create booking." });
        }

        var booking = result.Booking;
        return Ok(new GuestBookingCreateResponse(
            booking.Id,
            customer.Id,
            booking.Status,
            booking.Amount,
            booking.Currency));
    }
}
