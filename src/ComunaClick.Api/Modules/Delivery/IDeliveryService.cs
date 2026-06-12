using ComunaClick.Api.Modules.Delivery.Contracts;

namespace ComunaClick.Api.Modules.Delivery;

public interface IDeliveryService
{
    /// <summary>
    /// Registra un punto GPS del repartidor: valida rangos y orden temporal,
    /// omite la persistencia de puntos a menos de ~15 m del anterior (pero igual
    /// difunde por SignalR) y actualiza la posición actual del repartidor.
    /// </summary>
    Task<(bool Ok, string? Message)> UpdateCourierLocationAsync(
        Guid orderId,
        double lat,
        double lng,
        DateTimeOffset? timestamp,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cambia el estado del envío validando la máquina de estados; al llegar a
    /// delivered/canceled revoca el token del repartidor. Difunde el cambio.
    /// </summary>
    Task<(bool Ok, string? Message)> UpdateStatusAsync(
        Guid orderId,
        string status,
        CancellationToken cancellationToken = default);

    /// <summary>Estado completo del envío para pintar el mapa al cargar.</summary>
    Task<DeliverySnapshotResponse?> GetSnapshotAsync(Guid orderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asigna (o reasigna, regenerando la clave y revocando el link anterior)
    /// un repartidor a la orden y devuelve el link seguro para compartirle.
    /// </summary>
    Task<(AssignCourierResponse? Result, string? Error)> AssignCourierAsync(
        Guid orderId,
        Guid courierId,
        CancellationToken cancellationToken = default);
}
