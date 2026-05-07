using System.Text.Json;

namespace RazorIdentity.Models.Api;

public class PycClaveImportDto
{
    public string? Clave { get; set; }
}

public class PycImportUiDto
{
    public string? Clave { get; set; }
    /// <summary>vector | sap | c9 | av_fisico</summary>
    public string Kind { get; set; } = "";
    public string? ModoId { get; set; }
    public int ProyectoId { get; set; }
    public JsonElement Rows { get; set; }
}
