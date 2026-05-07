namespace RazorIdentity.Models.PmoFinance;

/// <summary>Payload JSON para la vista «Curva S - Parcial / Acum» (reporte1) con EVM, tabla por categoría y cascadas.</summary>
public sealed class Reporte1CurvaSEvmPackDto
{
    public bool EvmFechaOk { get; set; }
    public string? EvmFechaMensaje { get; set; }
    public IReadOnlyList<Reporte1CurvaPuntoDto> Curva { get; set; } = Array.Empty<Reporte1CurvaPuntoDto>();
    public Reporte1IndicadoresEvmDto? Indicadores { get; set; }
    public Reporte1AvanceFisicoDto? AvanceFisico { get; set; }
    public IReadOnlyList<Reporte1TablaCategoriaDto> TablaCategorias { get; set; } = Array.Empty<Reporte1TablaCategoriaDto>();
    public Reporte1TablaCategoriaTotalesDto? TablaTotales { get; set; }
    public Reporte1CascadaDto CascadaV0 { get; set; } = new();
    public Reporte1CascadaDto CascadaApi { get; set; } = new();
    public Reporte1IeacDto Ieac { get; set; } = new();
    public Reporte1EcdDto Ecd { get; set; } = new();
    public decimal BacTotalProyecto { get; set; }
}

public sealed class Reporte1CurvaPuntoDto
{
    public string Periodo { get; set; } = "";
    public decimal RealAcum { get; set; }
    public decimal V0Acum { get; set; }
    public decimal NpcAcum { get; set; }
    public decimal ApiAcum { get; set; }
}

public sealed class Reporte1IndicadoresEvmDto
{
    public string FechaSeguimiento { get; set; } = "";
    public decimal Ac { get; set; }
    public decimal Pv { get; set; }
    public decimal Ev { get; set; }
    public decimal Bac { get; set; }
    public decimal Cv { get; set; }
    public decimal Sv { get; set; }
    public decimal? CvPct { get; set; }
    public decimal? SvPct { get; set; }
    public decimal Cpi { get; set; }
    public decimal Spi { get; set; }
    public decimal Eac { get; set; }
    public decimal Etc { get; set; }
    public decimal Vac { get; set; }
    public decimal PctEv { get; set; }
    public decimal PctPv { get; set; }
    public decimal PctAc { get; set; }
    public string EstadoCosto { get; set; } = "";
    public string EstadoCronograma { get; set; } = "";
    public string EstadoGeneral { get; set; } = "";
}

public sealed class Reporte1AvanceFisicoDto
{
    public decimal RealPct { get; set; }
    public decimal ApiPct { get; set; }
    public decimal DesviacionPct { get; set; }
    public string Fuente { get; set; } = "";
}

public sealed class Reporte1TablaCategoriaDto
{
    public string Categoria { get; set; } = "";
    public decimal RealUsd { get; set; }
    public decimal V0Usd { get; set; }
    public decimal NpcUsd { get; set; }
    public decimal ApiUsd { get; set; }
    public decimal CascadaV0 { get; set; }
    public decimal CascadaApi { get; set; }
}

public sealed class Reporte1TablaCategoriaTotalesDto
{
    public decimal TotalReal { get; set; }
    public decimal TotalV0 { get; set; }
    public decimal TotalNpc { get; set; }
    public decimal TotalApi { get; set; }
    public decimal PctReal { get; set; }
    public decimal PctV0 { get; set; }
    public decimal PctNpc { get; set; }
    public decimal PctApi { get; set; }
}

public sealed class Reporte1CascadaDto
{
    public string Titulo { get; set; } = "";
    public IReadOnlyList<Reporte1CascadaBarDto> Barras { get; set; } = Array.Empty<Reporte1CascadaBarDto>();
}

public sealed class Reporte1CascadaBarDto
{
    public string Etiqueta { get; set; } = "";
    public string Tipo { get; set; } = ""; // inicio | delta | total
    public decimal BaseMs { get; set; }
    public decimal DeltaMs { get; set; }
    public string ColorDelta { get; set; } = "";
}

public sealed class Reporte1IeacDto
{
    public IReadOnlyList<Reporte1IeacItemDto> Metodologias { get; set; } = Array.Empty<Reporte1IeacItemDto>();
    public decimal Promedio { get; set; }
    public decimal Maximo { get; set; }
    public decimal Minimo { get; set; }
    public decimal TrabajoPorGanar { get; set; }
}

public sealed class Reporte1IeacItemDto
{
    public string Id { get; set; } = "";
    public string Etiqueta { get; set; } = "";
    public decimal Valor { get; set; }
}

public sealed class Reporte1EcdDto
{
    public IReadOnlyList<Reporte1EcdItemDto> Metodologias { get; set; } = Array.Empty<Reporte1EcdItemDto>();
    public string? FechaPromedio { get; set; }
    public string? FechaMaxima { get; set; }
    public string? FechaMinima { get; set; }
    public int? RangoMeses { get; set; }
}

public sealed class Reporte1EcdItemDto
{
    public string Id { get; set; } = "";
    public string Etiqueta { get; set; } = "";
    public string? FechaIso { get; set; }
}

public sealed class Reporte1CascadaDetalleDto
{
    public string Categoria { get; set; } = "";
    public string Tipo { get; set; } = ""; // V0 | API
    public decimal MontoObjetivoUsd { get; set; }
    public decimal TotalBaseUsd { get; set; }
    public decimal TotalRealUsd { get; set; }
    public decimal DiferenciaTotalUsd { get; set; }
    public decimal? DiferenciaTotalPct { get; set; }
    public string Conclusiones { get; set; } = "";
    public IReadOnlyList<Reporte1CascadaDetalleFilaDto> Filas { get; set; } = Array.Empty<Reporte1CascadaDetalleFilaDto>();
}

public sealed class Reporte1CascadaDetalleFilaDto
{
    public string Periodo { get; set; } = "";
    public decimal BaseUsd { get; set; }
    public decimal RealUsd { get; set; }
    public decimal DiferenciaUsd { get; set; }
    public decimal? DiferenciaPct { get; set; }
}
