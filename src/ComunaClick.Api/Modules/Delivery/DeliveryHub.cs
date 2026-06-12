using ComunaClick.Api.Modules.Orders;
using ComunaClick.Api.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ComunaClick.Api.Modules.Delivery;

/// <summary>
/// Hub de tracking en vivo. Los clientes (comprador o repartidor) se unen al
/// grupo de su pedido SOLO con un token válido; el servidor difunde
/// CourierLocationChanged y DeliveryStatusChanged a ese grupo.
/// </summary>
public sealed class DeliveryHub : Hub
{
    private readonly CoreDbContext _db;
    private readonly IOrderTrackingTokenService _trackingTokens;
    private readonly IDeliveryCourierTokenService _courierTokens;
    private readonly IDeliveryService _deliveryService;

    public DeliveryHub(
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

    public static string OrderGroup(Guid orderId) => $"order-{orderId:N}";

    /// <summary>Une la conexión al grupo del pedido tras validar el token (comprador o repartidor).</summary>
    public async Task JoinOrderTracking(Guid orderId, string token)
    {
        if (!await IsAuthorizedAsync(orderId, token))
        {
            throw new HubException("Token de seguimiento inválido para este pedido.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, OrderGroup(orderId));
    }

    public Task LeaveOrderTracking(Guid orderId)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, OrderGroup(orderId));

    /// <summary>Reporte de posición vía hub (alternativa al endpoint REST); solo con token de repartidor.</summary>
    public async Task SendCourierLocation(Guid orderId, double lat, double lng, DateTimeOffset? timestamp, string token)
    {
        if (!await IsCourierAuthorizedAsync(orderId, token))
        {
            throw new HubException("Token de repartidor inválido para este pedido.");
        }

        var result = await _deliveryService.UpdateCourierLocationAsync(orderId, lat, lng, timestamp);
        if (!result.Ok)
        {
            throw new HubException(result.Message ?? "No se pudo registrar la posición.");
        }
    }

    /// <summary>Cambio de estado vía hub (alternativa al endpoint REST); solo con token de repartidor.</summary>
    public async Task SendStatusUpdate(Guid orderId, string status, string token)
    {
        if (!await IsCourierAuthorizedAsync(orderId, token))
        {
            throw new HubException("Token de repartidor inválido para este pedido.");
        }

        var result = await _deliveryService.UpdateStatusAsync(orderId, status);
        if (!result.Ok)
        {
            throw new HubException(result.Message ?? "No se pudo cambiar el estado.");
        }
    }

    private async Task<bool> IsAuthorizedAsync(Guid orderId, string token)
    {
        // Token del comprador (tracking de la orden) o token del repartidor.
        if (_trackingTokens.TryValidate(token, orderId, out _))
        {
            return true;
        }

        return await IsCourierAuthorizedAsync(orderId, token);
    }

    private async Task<bool> IsCourierAuthorizedAsync(Guid orderId, string token)
    {
        var tokenKey = await _db.Orders.AsNoTracking()
            .Where(x => x.Id == orderId)
            .Select(x => x.CourierTokenKey)
            .FirstOrDefaultAsync();

        return _courierTokens.TryValidate(token, orderId, tokenKey);
    }
}
