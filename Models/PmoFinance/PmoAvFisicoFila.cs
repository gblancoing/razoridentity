namespace RazorIdentity.Models.PmoFinance;

/// <summary>Fila común de avance físico por escenario (tablas <c>av_fisico_*</c> en PostgreSQL).</summary>
public abstract class PmoAvFisicoFila
{
    public string Id { get; set; } = "";
    public int ProyectoId { get; set; }
    public DateOnly Periodo { get; set; }
    public string Vector { get; set; } = "";
    public decimal IeParcial { get; set; }
    public decimal IeAcumulado { get; set; }
    public decimal EmParcial { get; set; }
    public decimal EmAcumulado { get; set; }
    public decimal MoParcial { get; set; }
    public decimal MoAcumulado { get; set; }
    public decimal ApiParcial { get; set; }
    public decimal ApiAcum { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class PmoAvFisicoReal : PmoAvFisicoFila;
public class PmoAvFisicoNpc : PmoAvFisicoFila;
public class PmoAvFisicoPoa : PmoAvFisicoFila;
public class PmoAvFisicoV0 : PmoAvFisicoFila;
public class PmoAvFisicoApi : PmoAvFisicoFila;
