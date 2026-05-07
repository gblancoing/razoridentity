using System.Text.Json.Serialization;

namespace RazorIdentity.Models.Api;

/// <summary>DTO de respuesta de GET api/Usuarios/{userId}/Ficha (RIT_API).</summary>
public class UsuarioFichaApi
{
    public string UserId { get; set; } = "";
    public string? Turno { get; set; }

    /// <summary>Cargo del usuario (nCargo en la API).</summary>
    [JsonPropertyName("nCargo")]
    public string? NCargo { get; set; }

    public string? NombreCompleto { get; set; }
    public string? Email { get; set; }
}

/// <summary>DTO para PUT api/Usuarios/{userId}/Ficha (actualizar ficha en RIT_API).</summary>
public class UsuarioFichaApiUpdateDto
{
    public string? Turno { get; set; }

    [JsonPropertyName("nCargo")]
    public string? NCargo { get; set; }

    public string? NombreCompleto { get; set; }
    public string? Email { get; set; }
}

