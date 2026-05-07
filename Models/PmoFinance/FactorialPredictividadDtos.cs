namespace RazorIdentity.Models.PmoFinance;

public sealed class FactorialPredictividadResponseDto
{
    public string Titulo { get; set; } = "Predictividad";
    public string Subtitulo { get; set; } = "";
    public string Hasta20 { get; set; } = "";
    public string FiltroDescripcion { get; set; } = "";
    public FactorialPredictividadResumenDto Resumen { get; set; } = new();
    public IReadOnlyList<FactorialPredictividadHistItemDto> HistorialFinanciero { get; set; } = Array.Empty<FactorialPredictividadHistItemDto>();
    public IReadOnlyList<FactorialPredictividadHistItemDto> HistorialFisico { get; set; } = Array.Empty<FactorialPredictividadHistItemDto>();
    /// <summary>Fila mensual unificada (avance físico + financiero) para tabla histórica ejecutiva.</summary>
    public IReadOnlyList<FactorialPredictividadHistorialMensualDto> HistorialMensual { get; set; } = Array.Empty<FactorialPredictividadHistorialMensualDto>();
    public FactorialPredictividadHistorialPromediosDto HistorialPromedios { get; set; } = new();
    public IReadOnlyList<FactorialPredictividadTrendItemDto> TendenciaFinanciera { get; set; } = Array.Empty<FactorialPredictividadTrendItemDto>();
    public IReadOnlyList<FactorialPredictividadTrendItemDto> TendenciaFisica { get; set; } = Array.Empty<FactorialPredictividadTrendItemDto>();
}

public sealed class FactorialPredictividadResumenDto
{
    public decimal ProyeccionFinanciera { get; set; }
    public decimal RealFinanciera { get; set; }
    public decimal DesviacionFinancieraPct { get; set; }
    public decimal ProyeccionFisica { get; set; }
    public decimal RealFisica { get; set; }
    public decimal DesviacionFisicaPct { get; set; }
    public decimal PrecisionFinanciera { get; set; }
    public decimal PrecisionFisica { get; set; }
    public decimal PrecisionPromedio { get; set; }
    public int NotaFinanciera { get; set; }
    public int NotaFisica { get; set; }
}

public sealed class FactorialPredictividadHistItemDto
{
    public string Periodo { get; set; } = "";
    public decimal Valor { get; set; }
}

public sealed class FactorialPredictividadTrendItemDto
{
    public string Periodo { get; set; } = "";
    public decimal Proyeccion { get; set; }
    public decimal Real { get; set; }
    public decimal DesviacionPct { get; set; }
}

public sealed class FactorialPredictividadHistorialMensualDto
{
    public string PeriodoIso { get; set; } = "";
    /// <summary>Ej. ENE/2025</summary>
    public string PeriodoEtiqueta { get; set; } = "";
    public decimal FisProyeccion { get; set; }
    public decimal FisReal { get; set; }
    public decimal FisDesviacionPct { get; set; }
    public decimal FisPrecision { get; set; }
    public int FisNota { get; set; }
    public string FisNotaTexto { get; set; } = "";
    public decimal FinProyeccion { get; set; }
    public decimal FinReal { get; set; }
    public decimal FinDesviacionPct { get; set; }
    public decimal FinPrecision { get; set; }
    public int FinNota { get; set; }
    public string FinNotaTexto { get; set; } = "";
}

public sealed class FactorialPredictividadHistorialPromediosDto
{
    public decimal FisPromedioProyeccion { get; set; }
    public decimal FisPromedioReal { get; set; }
    public decimal FisPromedioDesviacionPct { get; set; }
    public decimal FisPromedioPrecision { get; set; }
    public decimal FisPromedioNota { get; set; }
    public decimal FinSumaProyeccion { get; set; }
    public decimal FinSumaReal { get; set; }
    public decimal FinDesviacionPct { get; set; }
    public decimal FinPromedioPrecision { get; set; }
    public decimal FinPromedioNota { get; set; }
}
