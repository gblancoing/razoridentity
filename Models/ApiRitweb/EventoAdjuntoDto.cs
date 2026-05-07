namespace RazorIdentity.Models.ApiRitweb;

public class EventoAdjuntoDto
{
    public int Id { get; set; }
    public int EventoId { get; set; }
    public string TipoAdjunto { get; set; } = ""; // FotoEvento | FotoCierre
    public string RutaArchivo { get; set; } = "";
    public string? NombreArchivo { get; set; }
    public string? ContentType { get; set; }
    public DateTime FechaCreacion { get; set; }
}
