namespace RazorIdentity.Models.ApiRitweb;

public class EventoDto
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    /// <summary>Nombre del usuario que registró el evento (si la API lo devuelve).</summary>
    public string? UsuarioNombre { get; set; }
    public int EmpresaColaboradoraId { get; set; }
    /// <summary>Nombre de la empresa colaboradora (si la API lo devuelve).</summary>
    public string? EmpresaColaboradoraNombre { get; set; }
    public int TipoEventoId { get; set; }
    public int? TipoEventoDetalleId { get; set; }
    public int ProyectoId { get; set; }
    public int? SectorId { get; set; }
    public int? DisciplinaId { get; set; }
    /// <summary>Fecha del evento (registro).</summary>
    public DateTime? FechaEvento { get; set; }
    /// <summary>Turno asignado al momento del registro.</summary>
    public string? Turno { get; set; }
    public int? TipoAlertaId { get; set; }
    public int? CriticidadId { get; set; }
    public string? ProfesionalNotificadoNombre { get; set; }
    public string? ProfesionalNotificadoCargo { get; set; }
    public bool RequiereCierre { get; set; }
    public bool RequiereDocumentoRespaldo { get; set; }
    public int? TipoDocumentoRespaldoId { get; set; }
    public string? ReferenciaDocumentoRespaldo { get; set; }
    public string? NumeroDocumentoProtocolo { get; set; }
    /// <summary>N° Código VP asociado al evento (almacenado en API_Ritweb).</summary>
    public string? CodigoVp { get; set; }
    /// <summary>Latitud del evento (API_Ritweb). Para mapas y georeferencia.</summary>
    public decimal? Latitude { get; set; }
    /// <summary>Longitud del evento (API_Ritweb). Para mapas y georeferencia.</summary>
    public decimal? Longitude { get; set; }
    public string? Descripcion { get; set; }
    public DateTime FechaCreacion { get; set; }
    public DateTime? FechaCierre { get; set; }
    public string? DescripcionCierre { get; set; }
    public List<EventoAdjuntoDto>? Adjuntos { get; set; }
}
