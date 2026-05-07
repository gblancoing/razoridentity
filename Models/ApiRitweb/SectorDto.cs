namespace RazorIdentity.Models.ApiRitweb;

public class SectorDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public int ProyectoId { get; set; }
    public string? Codigo { get; set; }
    public string? Descripcion { get; set; }
    public bool Activo { get; set; }
}
