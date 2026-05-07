namespace RazorIdentity.Models.ApiRitweb;

public class TipoDocumentoRespaldoDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public string? Codigo { get; set; }
    public int DisciplinaId { get; set; }
    public string? Descripcion { get; set; }
    public string? NumeroProtocolo { get; set; }
    public bool Activo { get; set; }
}
