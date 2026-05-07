using System.Text.Json;
using System.Text.Json.Serialization;

namespace RazorIdentity.Models.Api;

/// <summary>Respuesta tipo PHP <c>datos_financieros.php</c>: <c>{ success, datos: [] }</c>.</summary>
public class PycDatosFinancierosResponse
{
    public bool Success { get; set; }
    public List<PycVectorFinancieroRowDto> Datos { get; set; } = new();
}

/// <summary>Fila estándar vectores Real / V0 / NPC / API (tablas parciales y acumuladas).</summary>
public class PycVectorFinancieroRowDto
{
    public int Id { get; set; }
    public int ProyectoId { get; set; }
    public string? ProyectoNombre { get; set; }
    public string? CentroCosto { get; set; }
    public string? Periodo { get; set; }
    public string? Tipo { get; set; }
    public string? CatVp { get; set; }
    public string? DetalleFactorial { get; set; }
    public decimal Monto { get; set; }
}

public class PycImportacionRequestDto
{
    public int ProyectoId { get; set; }
    public List<Dictionary<string, object?>> Rows { get; set; } = new();
}

public class PycImportacionResponseDto
{
    public bool Success { get; set; }
    [JsonPropertyName("inserted")]
    public int? Inserted { get; set; }
    [JsonPropertyName("inserted_count")]
    public int? InsertedCount { get; set; }
}

/// <summary>Cuerpo POST compatible con PHP: <c>proyecto_id</c> + <c>rows</c>.</summary>
public class PycImportPostBody
{
    [JsonPropertyName("proyecto_id")]
    public int ProyectoId { get; set; }

    [JsonPropertyName("rows")]
    public List<JsonElement> Rows { get; set; } = new();
}

/// <summary>POST <c>importar_av_real_proyectado.php</c>: <c>proyecto_id</c>, <c>tabla</c>, <c>rows</c>.</summary>
public class PycAvFisicoImportPostBody
{
    [JsonPropertyName("proyecto_id")]
    public int ProyectoId { get; set; }

    [JsonPropertyName("tabla")]
    public string Tabla { get; set; } = "";

    [JsonPropertyName("rows")]
    public List<JsonElement> Rows { get; set; } = new();
}
