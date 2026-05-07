namespace RazorIdentity.Models.Api;

public class UsuarioAppApi
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public int AppId { get; set; }
    /// <summary>Proyecto asignado para esta app (ej. RitWeb). Si la API lo expone, se usa como proyecto por defecto para usuarios no super_admin.</summary>
    public int? ProyectoId { get; set; }
}
