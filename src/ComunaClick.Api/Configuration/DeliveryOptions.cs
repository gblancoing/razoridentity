namespace ComunaClick.Api.Configuration;

public sealed class DeliveryOptions
{
    public const string SectionName = "Delivery";

    /// <summary>
    /// Tarifa plana provisional del despacho. Si es null o 0 se usa el BaseFee
    /// del proveedor (comportamiento histórico). El cálculo real por distancia
    /// se implementará detrás de IDeliveryPricingService en una tarea posterior.
    /// </summary>
    public decimal? FlatFee { get; set; }

    /// <summary>Vigencia del link/token del repartidor (24-48h recomendado).</summary>
    public int CourierTokenTtlHours { get; set; } = 48;

    /// <summary>Base pública de la app para armar el link del repartidor.</summary>
    public string AppBaseUrl { get; set; } = "https://app.comunaclic.cl";

    /// <summary>Distancia mínima (metros) entre puntos persistidos del historial GPS.</summary>
    public double MinPersistDistanceMeters { get; set; } = 15;

    /// <summary>Días que se conserva el historial GPS tras entregar/cancelar (se conserva el último punto).</summary>
    public int TrackingRetentionDays { get; set; } = 7;
}
