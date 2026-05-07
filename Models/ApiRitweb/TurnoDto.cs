namespace RazorIdentity.Models.ApiRitweb;

/// <summary>Catálogo de turnos (Mañana, Tarde, Noche, etc.) en API_Ritweb. El turno asignado al usuario se obtiene de la ficha (RIT_API).</summary>
public class TurnoDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public string? Codigo { get; set; }
    public int Orden { get; set; }
    public bool Activo { get; set; }
}
