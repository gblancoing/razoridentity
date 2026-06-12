using ComunaClick.Api.Modules.Delivery.Contracts;
using ComunaClick.Api.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace ComunaClick.Api.Modules.Delivery;

/// <summary>
/// Endpoints del repartidor (sin cuenta): autorizados únicamente por el token
/// HMAC del link que el vendedor le compartió. El token embebe la clave de
/// asignación vigente, por lo que reasignar/entregar/cancelar lo revoca.
/// </summary>
[ApiController]
[Route("api/courier")]
public sealed class CourierController : ControllerBase
{
    private readonly CoreDbContext _db;
    private readonly IDeliveryCourierTokenService _courierTokens;
    private readonly IDeliveryService _deliveryService;

    public CourierController(
        CoreDbContext db,
        IDeliveryCourierTokenService courierTokens,
        IDeliveryService deliveryService)
    {
        _db = db;
        _courierTokens = courierTokens;
        _deliveryService = deliveryService;
    }

    /// <summary>Recibe la posición GPS, la persiste y la difunde al grupo del pedido.</summary>
    [AllowAnonymous]
    [HttpPost("location")]
    [EnableRateLimiting("courier-gps")]
    public async Task<IActionResult> Location(CourierLocationRequest request)
    {
        if (!await IsCourierAuthorizedAsync(request.OrderId, request.Token))
        {
            return Unauthorized(new { message = "Token de repartidor inválido o vencido." });
        }

        var result = await _deliveryService.UpdateCourierLocationAsync(
            request.OrderId,
            request.Lat,
            request.Lng,
            request.Timestamp,
            HttpContext.RequestAborted);

        return result.Ok ? Ok(new { ok = true }) : BadRequest(new { message = result.Message });
    }

    /// <summary>Cambia el estado del envío ("Recogí el pedido" / "Entregué") y notifica.</summary>
    [AllowAnonymous]
    [HttpPost("status")]
    [EnableRateLimiting("courier-gps")]
    public async Task<IActionResult> Status(CourierStatusRequest request)
    {
        if (!await IsCourierAuthorizedAsync(request.OrderId, request.Token))
        {
            return Unauthorized(new { message = "Token de repartidor inválido o vencido." });
        }

        // El repartidor solo puede avanzar el envío, nunca cancelarlo ni retrocederlo.
        var target = DeliveryStatuses.Normalize(request.Status);
        if (target is not (DeliveryStatuses.PickedUp or DeliveryStatuses.InTransit or DeliveryStatuses.Delivered))
        {
            return BadRequest(new { message = "Estado no permitido para el repartidor." });
        }

        var result = await _deliveryService.UpdateStatusAsync(request.OrderId, target, HttpContext.RequestAborted);
        return result.Ok ? Ok(new { ok = true, status = target }) : BadRequest(new { message = result.Message });
    }

    private async Task<bool> IsCourierAuthorizedAsync(Guid orderId, string token)
    {
        var tokenKey = await _db.Orders.AsNoTracking()
            .Where(x => x.Id == orderId)
            .Select(x => x.CourierTokenKey)
            .FirstOrDefaultAsync(HttpContext.RequestAborted);

        return _courierTokens.TryValidate(token, orderId, tokenKey);
    }
}
