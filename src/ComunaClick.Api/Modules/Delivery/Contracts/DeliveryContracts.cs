namespace ComunaClick.Api.Modules.Delivery.Contracts;

/// <summary>Reporte de posición GPS del repartidor (token de repartidor obligatorio).</summary>
public sealed record CourierLocationRequest(
    Guid OrderId,
    double Lat,
    double Lng,
    DateTimeOffset? Timestamp,
    string Token);

/// <summary>Cambio de estado del envío desde la vista del repartidor.</summary>
public sealed record CourierStatusRequest(
    Guid OrderId,
    string Status,
    string Token);

public sealed record CourierCreateRequest(string Name, string Phone, string? Company, string? Email = null);

public sealed record CourierUpdateRequest(string? Name, string? Phone, string? Company, bool? IsAvailable, string? Email = null);

public sealed record CourierResponse(
    Guid Id,
    Guid PartnerId,
    string Name,
    string Phone,
    string? Company,
    bool IsAvailable,
    string? Email = null,
    Guid? UserId = null);

public sealed record AssignCourierRequest(Guid CourierId);

public sealed record AssignCourierResponse(
    Guid OrderId,
    Guid CourierId,
    string CourierName,
    string CourierPhone,
    string CourierLink,
    string DeliveryStatus);

/// <summary>
/// Estado completo del envío para pintar el mapa al cargar (el marcador del
/// repartidor parte de la última ubicación persistida, no del origen).
/// </summary>
public sealed record DeliverySnapshotResponse(
    Guid OrderId,
    string? DeliveryStatus,
    string? DeliveryType,
    double? OriginLat,
    double? OriginLng,
    string? OriginAddress,
    double? DestinationLat,
    double? DestinationLng,
    string? DestinationAddress,
    string? CourierName,
    string? CourierPhone,
    string? BuyerName,
    string? BuyerPhone,
    double? LastLat,
    double? LastLng,
    DateTimeOffset? LastTimestamp);
