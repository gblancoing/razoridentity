using ComunaClick.Api.Modules.Orders.Contracts;
using ComunaClick.Api.Persistence.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ComunaClick.Api.Modules.Orders;

[ApiController]
[Route("v1/public/orders")]
public sealed class PublicOrdersController : ControllerBase
{
    private readonly IOrderCheckoutService _orderCheckoutService;
    private readonly IOrderTrackingTokenService _trackingTokens;

    public PublicOrdersController(
        IOrderCheckoutService orderCheckoutService,
        IOrderTrackingTokenService trackingTokens)
    {
        _orderCheckoutService = orderCheckoutService;
        _trackingTokens = trackingTokens;
    }

    [AllowAnonymous]
    [EnableRateLimiting("public-write")]
    [HttpPost("guest")]
    public async Task<ActionResult<GuestOrderCreateResponse>> CreateGuest(
        GuestOrderCreateRequest request,
        CancellationToken cancellationToken)
    {
        var (customer, result) = await _orderCheckoutService.CreateGuestOrderAsync(request, cancellationToken);
        if (!result.Success || result.Order is null || customer is null)
        {
            return BadRequest(new { message = result.ErrorMessage ?? "Could not create order." });
        }

        var order = result.Order;
        return Ok(new GuestOrderCreateResponse(
            order.Id,
            customer.Id,
            order.Status,
            order.TotalAmount,
            order.Currency,
            _trackingTokens.Create(order.Id, customer.Id)));
    }
}
