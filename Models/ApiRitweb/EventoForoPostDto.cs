namespace RazorIdentity.Models.ApiRitweb;

/// <summary>Publicación en el foro de seguimiento de un evento (registro de gestiones para cierre de alertas).</summary>
public class EventoForoPostDto
{
    public int Id { get; set; }
    public int EventoId { get; set; }
    public string UserId { get; set; } = "";
    public string? UsuarioNombre { get; set; }
    public string? UsuarioCargo { get; set; }
    public DateTime FechaCreacion { get; set; }
    public string Contenido { get; set; } = "";
    /// <summary>Si es true, se destaca como actualización prioritaria.</summary>
    public bool EsPrioridad { get; set; }
    /// <summary>ID del post padre (respuesta). Null = mensaje raíz.</summary>
    public int? ParentId { get; set; }
    /// <summary>Respuestas anidadas (opcional, según API).</summary>
    public List<EventoForoPostDto>? Respuestas { get; set; }
}
