namespace RazorIdentity.Models.ApiRitweb;

public class TipoEventoDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    /// <summary>Si está vacío o null, el tipo aplica a todas las disciplinas.</summary>
    public List<int>? DisciplinaIds { get; set; }
    public bool RequiereCierre { get; set; }
    public bool RequiereDocumentoRespaldo { get; set; }
    public bool Activo { get; set; }
}
