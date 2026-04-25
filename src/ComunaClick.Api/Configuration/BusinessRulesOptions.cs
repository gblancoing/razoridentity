namespace ComunaClick.Api.Configuration;

/// <summary>
/// Parámetros de negocio alineados al BPMN (comisiones segmento A/B, no-show).
/// Consumir desde servicios de dominio cuando se implementen liquidaciones y cancelaciones.
/// </summary>
public sealed class BusinessRulesOptions
{
    public const string SectionName = "BusinessRules";

    /// <summary>Comisión mínima sugerida para reservas de servicios (segmento B), porcentaje.</summary>
    public decimal ServiceCommissionMinPercent { get; set; } = 3m;

    /// <summary>Comisión máxima sugerida para reservas de servicios (segmento B), porcentaje.</summary>
    public decimal ServiceCommissionMaxPercent { get; set; } = 5m;

    /// <summary>Cargo por no-show / cancelación tardía (referencia BPMN), porcentaje sobre el valor de la reserva.</summary>
    public decimal ServiceNoShowChargePercent { get; set; } = 4m;

    /// <summary>Horas antes del turno sin cargo al cancelar (política sugerida).</summary>
    public int ServiceFreeCancelHoursBefore { get; set; } = 24;

    /// <summary>Comisión mínima ventas de productos (segmento A), porcentaje.</summary>
    public decimal ProductCommissionMinPercent { get; set; } = 10m;

    /// <summary>Comisión máxima ventas de productos (segmento A), porcentaje.</summary>
    public decimal ProductCommissionMaxPercent { get; set; } = 18m;
}
