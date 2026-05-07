namespace RazorIdentity.Models.PmoFinance;

/// <summary>Flujo financiero SAP (extracto MO–CT). Paridad con tabla MySQL <c>financiero_sap</c>.</summary>
public class PmoFinancieroSap
{
    public int Id { get; set; }
    public string IdSap { get; set; } = "";
    public int ProyectoId { get; set; }
    public string? CentroCostoNombre { get; set; }
    public string? VersionSap { get; set; }
    public string? Descripcion { get; set; }
    public string? GrupoVersion { get; set; }
    public DateOnly? Periodo { get; set; }
    public decimal Mo { get; set; }
    public decimal Ic { get; set; }
    public decimal Em { get; set; }
    public decimal Ie { get; set; }
    public decimal Sc { get; set; }
    public decimal Ad { get; set; }
    public decimal Cl { get; set; }
    public decimal Ct { get; set; }
    public DateTime FechaCreacion { get; set; }
    public DateTime FechaActualizacion { get; set; }
}
