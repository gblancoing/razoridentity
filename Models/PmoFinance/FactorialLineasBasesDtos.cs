namespace RazorIdentity.Models.PmoFinance;

public sealed class FactorialLineasBasesResponseDto
{
    public string Titulo { get; set; } = "Líneas Bases - Real/Proyectado";
    public string Subtitulo { get; set; } = "";
    public string? PeriodoLineaCorte { get; set; }
    public IReadOnlyList<string> Vectores { get; set; } = Array.Empty<string>();
    public FactorialLineasBasesCardsDto Cards { get; set; } = new();
    public FactorialLineasBasesChartDto Chart { get; set; } = new();
    public FactorialLineasBasesTableDto Table { get; set; } = new();
}

public sealed class FactorialLineasBasesCardsDto
{
    public FactorialLineasBasesCardItemDto Real { get; set; } = new();
    public FactorialLineasBasesCardItemDto Npc { get; set; } = new();
    public FactorialLineasBasesCardItemDto Poa { get; set; } = new();
    public FactorialLineasBasesCardItemDto V0 { get; set; } = new();
    public FactorialLineasBasesCardItemDto Api { get; set; } = new();
}

public sealed class FactorialLineasBasesCardItemDto
{
    public decimal ApiAcum { get; set; }
    public decimal ApiParcial { get; set; }
}

public sealed class FactorialLineasBasesChartDto
{
    public IReadOnlyList<FactorialLineasBasesChartPointDto> Series { get; set; } = Array.Empty<FactorialLineasBasesChartPointDto>();
}

public sealed class FactorialLineasBasesChartPointDto
{
    public string Periodo { get; set; } = "";
    public decimal Real { get; set; }
    public decimal Npc { get; set; }
    public decimal Poa { get; set; }
    public decimal V0 { get; set; }
    public decimal Api { get; set; }
}

public sealed class FactorialLineasBasesTableDto
{
    public string TablaVisualizar { get; set; } = "todas";
    public int TotalRegistros { get; set; }
    public IReadOnlyList<FactorialLineasBasesTableItemDto> Items { get; set; } = Array.Empty<FactorialLineasBasesTableItemDto>();
}

public sealed class FactorialLineasBasesTableItemDto
{
    public string? Tipo { get; set; }
    public string Id { get; set; } = "";
    public string Vector { get; set; } = "";
    public string Periodo { get; set; } = "";
    public decimal IeParcial { get; set; }
    public decimal IeAcumulado { get; set; }
    public decimal EmParcial { get; set; }
    public decimal EmAcumulado { get; set; }
    public decimal MoParcial { get; set; }
    public decimal MoAcumulado { get; set; }
    public decimal ApiParcial { get; set; }
    public decimal ApiAcum { get; set; }
}
