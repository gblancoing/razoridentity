using System.Security.Cryptography;
using ComunaClick.Api.Configuration;
using ComunaClick.Api.Modules.Delivery.Contracts;
using ComunaClick.Api.Persistence;
using ComunaClick.Api.Persistence.Entities;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ComunaClick.Api.Modules.Delivery;

public sealed class DeliveryService : IDeliveryService
{
    private readonly CoreDbContext _db;
    private readonly IHubContext<DeliveryHub> _hub;
    private readonly IDeliveryCourierTokenService _courierTokens;
    private readonly DeliveryOptions _options;

    public DeliveryService(
        CoreDbContext db,
        IHubContext<DeliveryHub> hub,
        IDeliveryCourierTokenService courierTokens,
        IOptions<DeliveryOptions> options)
    {
        _db = db;
        _hub = hub;
        _courierTokens = courierTokens;
        _options = options.Value;
    }

    public async Task<(bool Ok, string? Message)> UpdateCourierLocationAsync(
        Guid orderId,
        double lat,
        double lng,
        DateTimeOffset? timestamp,
        CancellationToken cancellationToken = default)
    {
        // Coordenadas plausibles; descarta basura del GPS o payloads malformados.
        if (lat is < -90 or > 90 || lng is < -180 or > 180)
        {
            return (false, "Coordenadas fuera de rango.");
        }

        var order = await _db.Orders.FirstOrDefaultAsync(x => x.Id == orderId, cancellationToken);
        if (order is null || order.CourierId is null)
        {
            return (false, "El pedido no tiene un envío con repartidor asignado.");
        }

        if (!DeliveryStatuses.IsActive(order.DeliveryStatus))
        {
            return (false, "El envío ya finalizó: no se aceptan más posiciones.");
        }

        var now = DateTimeOffset.UtcNow;
        var pointAt = timestamp?.ToUniversalTime() ?? now;
        if (pointAt > now)
        {
            // Relojes adelantados del dispositivo: se normaliza al reloj del servidor.
            pointAt = now;
        }

        var last = await _db.DeliveryTrackings
            .Where(x => x.OrderId == orderId)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        // Puntos que llegan fuera de orden (más antiguos que el último persistido) se descartan.
        if (last is not null && timestamp.HasValue && pointAt <= last.CreatedAt)
        {
            return (true, null);
        }

        var status = DeliveryStatuses.Normalize(order.DeliveryStatus);

        // Difundir siempre (el mapa se mueve fluido) aunque el punto no se persista.
        await BroadcastLocationAsync(orderId, lat, lng, pointAt, cancellationToken);

        // Persistencia con umbral de distancia: evita llenar el historial con
        // puntos casi idénticos cuando el repartidor está detenido.
        if (last is not null && DistanceMeters(last.Lat, last.Lng, lat, lng) < _options.MinPersistDistanceMeters)
        {
            return (true, null);
        }

        _db.DeliveryTrackings.Add(new DeliveryTracking
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            CourierId = order.CourierId.Value,
            Lat = lat,
            Lng = lng,
            Status = status,
            CreatedAt = pointAt
        });

        var courier = await _db.Couriers.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == order.CourierId.Value, cancellationToken);
        if (courier is not null)
        {
            courier.CurrentLat = lat;
            courier.CurrentLng = lng;
            courier.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    public async Task<(bool Ok, string? Message)> UpdateStatusAsync(
        Guid orderId,
        string status,
        CancellationToken cancellationToken = default)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(x => x.Id == orderId, cancellationToken);
        if (order is null)
        {
            return (false, "Pedido no encontrado.");
        }

        var current = DeliveryStatuses.Normalize(order.DeliveryStatus ?? DeliveryStatuses.Pending);
        var target = DeliveryStatuses.Normalize(status);

        // "Recogí el pedido" salta directo a in_transit: se registra el paso
        // intermedio picked_up y se difunden ambos cambios.
        if (string.Equals(target, DeliveryStatuses.InTransit, StringComparison.Ordinal)
            && string.Equals(current, DeliveryStatuses.Assigned, StringComparison.Ordinal))
        {
            var step = await ApplyStatusAsync(order, DeliveryStatuses.PickedUp, cancellationToken);
            if (!step.Ok)
            {
                return step;
            }

            current = DeliveryStatuses.PickedUp;
        }

        if (string.Equals(current, target, StringComparison.Ordinal))
        {
            return (true, null);
        }

        return await ApplyStatusAsync(order, target, cancellationToken);
    }

    private async Task<(bool Ok, string? Message)> ApplyStatusAsync(
        Order order,
        string target,
        CancellationToken cancellationToken)
    {
        if (!DeliveryStatuses.CanTransition(order.DeliveryStatus ?? DeliveryStatuses.Pending, target, out var error))
        {
            return (false, error);
        }

        order.DeliveryStatus = target;
        order.UpdatedAt = DateTimeOffset.UtcNow;

        // Estados terminales: se revoca el token del repartidor (la clave de
        // asignación deja de existir y ningún token previo vuelve a validar).
        if (target is DeliveryStatuses.Delivered or DeliveryStatuses.Canceled)
        {
            order.CourierTokenKey = null;
        }

        await _db.SaveChangesAsync(cancellationToken);
        await _hub.Clients.Group(DeliveryHub.OrderGroup(order.Id))
            .SendAsync("DeliveryStatusChanged", new { status = target }, cancellationToken);
        return (true, null);
    }

    public async Task<DeliverySnapshotResponse?> GetSnapshotAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var order = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(x => x.Id == orderId, cancellationToken);
        if (order is null)
        {
            return null;
        }

        Courier? courier = null;
        if (order.CourierId is { } courierId)
        {
            courier = await _db.Couriers.IgnoreQueryFilters().AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == courierId, cancellationToken);
        }

        var last = await _db.DeliveryTrackings.AsNoTracking()
            .Where(x => x.OrderId == orderId)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        // Contacto del comprador (lo necesita el repartidor para coordinar la entrega).
        var buyerPhone = await _db.Customers.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.Id == order.CustomerId)
            .Select(x => x.Phone)
            .FirstOrDefaultAsync(cancellationToken);

        return new DeliverySnapshotResponse(
            order.Id,
            DeliveryStatuses.Normalize(order.DeliveryStatus ?? DeliveryStatuses.Pending),
            order.DeliveryType,
            order.OriginLat,
            order.OriginLng,
            order.OriginAddress,
            order.DestinationLat,
            order.DestinationLng,
            order.DeliveryAddress,
            courier?.Name,
            courier?.Phone,
            order.BuyerName,
            buyerPhone,
            last?.Lat,
            last?.Lng,
            last?.CreatedAt);
    }

    public async Task<(AssignCourierResponse? Result, string? Error)> AssignCourierAsync(
        Guid orderId,
        Guid courierId,
        CancellationToken cancellationToken = default)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(x => x.Id == orderId, cancellationToken);
        if (order is null)
        {
            return (null, "Pedido no encontrado.");
        }

        var courier = await _db.Couriers.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == courierId && x.PartnerId == order.PartnerId, cancellationToken);
        if (courier is null)
        {
            return (null, "El repartidor no existe o no pertenece a este negocio.");
        }

        var current = DeliveryStatuses.Normalize(order.DeliveryStatus ?? DeliveryStatuses.Pending);
        if (!DeliveryStatuses.IsActive(current))
        {
            return (null, "El envío ya finalizó: no se puede asignar repartidor.");
        }

        // Asignar/reasignar regenera la clave: el link anterior queda revocado
        // (solo existe 1 token activo por orden).
        order.CourierId = courier.Id;
        order.CourierTokenKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        order.DeliveryType ??= DeliveryTypes.Delivery;
        if (string.Equals(current, DeliveryStatuses.Pending, StringComparison.Ordinal))
        {
            order.DeliveryStatus = DeliveryStatuses.Assigned;
        }

        order.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        await _hub.Clients.Group(DeliveryHub.OrderGroup(order.Id))
            .SendAsync("DeliveryStatusChanged", new { status = DeliveryStatuses.Normalize(order.DeliveryStatus) }, cancellationToken);

        var token = _courierTokens.Create(order.Id, order.CourierTokenKey);
        var link = $"{_options.AppBaseUrl.TrimEnd('/')}/courier/delivery/{order.Id}?token={Uri.EscapeDataString(token)}";

        return (new AssignCourierResponse(
            order.Id,
            courier.Id,
            courier.Name,
            courier.Phone,
            link,
            DeliveryStatuses.Normalize(order.DeliveryStatus)), null);
    }

    private Task BroadcastLocationAsync(Guid orderId, double lat, double lng, DateTimeOffset timestamp, CancellationToken cancellationToken)
        => _hub.Clients.Group(DeliveryHub.OrderGroup(orderId))
            .SendAsync("CourierLocationChanged", new { lat, lng, timestamp }, cancellationToken);

    /// <summary>Distancia Haversine en metros entre dos coordenadas.</summary>
    public static double DistanceMeters(double lat1, double lng1, double lat2, double lng2)
    {
        const double earthRadiusMeters = 6371000d;
        var dLat = (lat2 - lat1) * Math.PI / 180d;
        var dLng = (lng2 - lng1) * Math.PI / 180d;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                + Math.Cos(lat1 * Math.PI / 180d) * Math.Cos(lat2 * Math.PI / 180d)
                * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return 2 * earthRadiusMeters * Math.Asin(Math.Min(1d, Math.Sqrt(a)));
    }
}
