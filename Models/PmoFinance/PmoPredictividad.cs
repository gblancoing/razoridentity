namespace RazorIdentity.Models.PmoFinance;

public sealed class PmoPredictividad
{
    public int IdPredictivo { get; set; }
    public int ProyectoId { get; set; }
    public int? CentroCostoId { get; set; }
    public DateOnly PeriodoPrediccion { get; set; }
    public decimal PorcentajePredicido { get; set; }
    public DateOnly? PeriodoCierreReal { get; set; }
    public decimal ValorRealPorcentaje { get; set; }
}
