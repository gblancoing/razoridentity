namespace RazorIdentity.Models.ApiRitweb;

public class TipoAlertaDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public string? Codigo { get; set; }
    public int Orden { get; set; }
    public bool Activo { get; set; }
}

