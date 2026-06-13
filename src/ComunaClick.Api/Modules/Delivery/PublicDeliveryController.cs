using ComunaClick.Api.Modules.Delivery.Contracts;
using ComunaClick.Api.Modules.Orders;
using ComunaClick.Api.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace ComunaClick.Api.Modules.Delivery;

/// <summary>
/// Snapshot público del envío: lo usan la página /track del comprador (token
/// de tracking) y la vista del repartidor (token de courier). También sirve de
/// fallback por polling cuando SignalR no logra conectar.
/// </summary>
[ApiController]
[Route("v1/public/delivery")]
public sealed class PublicDeliveryController : ControllerBase
{
    private readonly CoreDbContext _db;
    private readonly IOrderTrackingTokenService _trackingTokens;
    private readonly IDeliveryCourierTokenService _courierTokens;
    private readonly IDeliveryService _deliveryService;

    public PublicDeliveryController(
        CoreDbContext db,
        IOrderTrackingTokenService trackingTokens,
        IDeliveryCourierTokenService courierTokens,
        IDeliveryService deliveryService)
    {
        _db = db;
        _trackingTokens = trackingTokens;
        _courierTokens = courierTokens;
        _deliveryService = deliveryService;
    }

    [AllowAnonymous]
    [HttpGet("{orderId:guid}")]
    [EnableRateLimiting("public-read")]
    public async Task<ActionResult<DeliverySnapshotResponse>> GetSnapshot(Guid orderId, [FromQuery] string? token)
    {
        if (!await IsAuthorizedAsync(orderId, token))
        {
            return Unauthorized(new { message = "Token de seguimiento inválido para este pedido." });
        }

        var snapshot = await _deliveryService.GetSnapshotAsync(orderId, HttpContext.RequestAborted);
        return snapshot is null ? NotFound() : Ok(snapshot);
    }

    private async Task<bool> IsAuthorizedAsync(Guid orderId, string? token)
    {
        if (_trackingTokens.TryValidate(token, orderId, out _))
        {
            return true;
        }

        var tokenKey = await _db.Orders.AsNoTracking()
            .Where(x => x.Id == orderId)
            .Select(x => x.CourierTokenKey)
            .FirstOrDefaultAsync(HttpContext.RequestAborted);

        return _courierTokens.TryValidate(token, orderId, tokenKey);
    }
}
