namespace RazorIdentity.Models.PmoFinance;

public sealed class FactorialEficienciaResponseDto
{
    public string MesCorte { get; set; } = "";
    public string Titulo { get; set; } = "Eficiencia del Gasto Fisico - Financiero";
    public string Subtitulo { get; set; } = "";
    public string FuenteFisica { get; set; } = "av_fisico_v0 + av_fisico_real";
    public IReadOnlyList<FactorialEficienciaFilaDto> Filas { get; set; } = Array.Empty<FactorialEficienciaFilaDto>();
    public IReadOnlyList<FactorialEficienciaFilaDto> HistoricoMensual { get; set; } = Array.Empty<FactorialEficienciaFilaDto>();
    public FactorialEficienciaFilaDto? PromedioHistorico { get; set; }
}

public sealed class FactorialEficienciaFilaDto
{
    public string Periodo { get; set; } = "";
    public string? MesIso { get; set; }
    public decimal PlanV0Usd { get; set; }
    public decimal GastoRealUsd { get; set; }
    public decimal CumplimientoA { get; set; }
    public decimal ProgV0Pct { get; set; }
    public decimal AvanceFisicoPct { get; set; }
    public decimal CumplimientoB { get; set; }
    public decimal EficienciaGasto { get; set; }
    public decimal Nota { get; set; }
}
