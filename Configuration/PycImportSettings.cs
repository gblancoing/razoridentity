namespace RazorIdentity.Configuration;

/// <summary>Clave para autorizar importaciones Excel (solo servidor; no exponer en el cliente).</summary>
public class PycImportSettings
{
    public const string SectionName = "PycImport";
    /// <summary>Si está vacío, las importaciones rechazan con mensaje de configuración pendiente.</summary>
    public string Clave { get; set; } = "";
}
