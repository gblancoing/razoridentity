namespace RazorIdentity.Models.ApiRitweb;

public class TipoEventoDetalleDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public int TipoEventoId { get; set; }
    public int DisciplinaId { get; set; }
    /// <summary>Null = nivel 1; con valor = nivel 2 o 3 (id del detalle padre).</summary>
    public int? TipoEventoDetallePadreId { get; set; }
    /// <summary>1, 2 o 3 (calculado por la API).</summary>
    public int Nivel { get; set; }
    public string? Codigo { get; set; }
    public bool Activo { get; set; }
    /// <summary>Hijos anidados cuando se usa GET con enArbol=true.</summary>
    public List<TipoEventoDetalleDto>? Hijos { get; set; }
}
