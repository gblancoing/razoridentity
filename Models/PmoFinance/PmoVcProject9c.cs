namespace RazorIdentity.Models.PmoFinance;

/// <summary>Project 9C. Sin FK a <c>campo3_fase</c> (catálogo VP puede validarse en aplicación).</summary>
public class PmoVcProject9c
{
    public string IdC9 { get; set; } = "";
    public DateOnly Periodo { get; set; }
    public string CatVp { get; set; } = "";
    public int MonedaBase { get; set; } = 2025;
    public int ProyectoId { get; set; }
    public decimal Base { get; set; }
    public decimal Cambio { get; set; }
    public decimal Control { get; set; }
    public decimal Tendencia { get; set; }
    public decimal Eat { get; set; }
    public decimal Compromiso { get; set; }
    public decimal Incurrido { get; set; }
    public decimal Financiero { get; set; }
    public decimal PorComprometer { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
