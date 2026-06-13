namespace ComunaClick.Api.Configuration;

/// <summary>
/// Parámetros del cálculo dinámico del costo de transporte. TODOS los valores
/// vienen de configuración (appsettings / variables de entorno), nunca del
/// cliente ni hardcodeados en código.
/// </summary>
public sealed class DeliveryPricingOptions
{
    public const string SectionName = "DeliveryPricing";

    /// <summary>Si está apagado se usa el fallback plano (BaseFee del proveedor / Delivery:FlatFee).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Zona horaria IANA para decidir el perfil horario (hora local de Chile, no UTC).</summary>
    public string TimeZone { get; set; } = "America/Santiago";

    /// <summary>Tope máximo de la tarifa (protege contra coordenadas erróneas). 0 = sin tope.</summary>
    public decimal MaxFee { get; set; } = 15000m;

    /// <summary>Radio máximo de cobertura en km; si la distancia lo excede, el envío no está disponible. 0 = sin límite.</summary>
    public double MaxDistanceKm { get; set; } = 15d;

    /// <summary>
    /// Perfiles horarios. Extensible a futuras franjas/tipos de transportista
    /// (taxis, etc.): se elige el primer perfil cuya ventana contiene la hora local.
    /// </summary>
    public List<DeliveryPricingProfile> Profiles { get; set; } =
    [
        // Valores por defecto = ejemplo del negocio; producción los define en appsettings.
        new() { Name = "nocturno", FromHour = 0, ToHour = 7, BaseRadiusKm = 2d, BaseFee = 3500m, PerKmFee = 1600m },
        new() { Name = "diurno", FromHour = 7, ToHour = 24, BaseRadiusKm = 2d, BaseFee = 2800m, PerKmFee = 1250m }
    ];

    /// <summary>Comisión ComunaClic sobre el monto de transporte (mismo modelo fijo + % del comercio).</summary>
    public DeliveryCommissionOptions Commission { get; set; } = new();
}

public sealed class DeliveryPricingProfile
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Hora local de inicio (inclusive, 0-23).</summary>
    public int FromHour { get; set; }

    /// <summary>Hora local de fin (exclusiva, 1-24). Ej.: nocturno 0→7, diurno 7→24.</summary>
    public int ToHour { get; set; } = 24;

    /// <summary>Radio en km cubierto por el valor base.</summary>
    public double BaseRadiusKm { get; set; } = 2d;

    /// <summary>Valor base dentro del radio.</summary>
    public decimal BaseFee { get; set; }

    /// <summary>Valor por km (o fracción: se redondea hacia arriba) que excede el radio base.</summary>
    public decimal PerKmFee { get; set; }

    public bool ContainsHour(int hour) => hour >= FromHour && hour < ToHour;
}

public sealed class DeliveryCommissionOptions
{
    /// <summary>Componente fijo en CLP de la comisión ComunaClic del transporte.</summary>
    public decimal FixedFeeAmount { get; set; }

    /// <summary>Componente porcentual (ej. 10 = 10%) de la comisión ComunaClic del transporte.</summary>
    public decimal PercentageFee { get; set; } = 10m;
}
