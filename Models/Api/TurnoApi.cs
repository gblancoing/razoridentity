namespace RazorIdentity.Models.Api;

/// <summary>Catálogo de turnos en RIT_API (ej. 5x2, 7x7, 14x14). El turno asignado al usuario está en la ficha (api/Usuarios/{id}/Ficha).</summary>
public class TurnoApi
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public string? Codigo { get; set; }
    public int Orden { get; set; }
    public bool Activo { get; set; }
}
