namespace ComunaClick.Api.Persistence.Entities;

/// <summary>
/// Punto del historial de un envío: posición GPS del repartidor y estado del
/// momento. La última fila por orden alimenta el mapa al recargar la página.
/// </summary>
public sealed class DeliveryTracking
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Guid CourierId { get; set; }
    public double Lat { get; set; }
    public double Lng { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
