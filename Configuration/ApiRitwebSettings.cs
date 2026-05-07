namespace RazorIdentity.Configuration;

/// <summary>
/// Configuración de la URL base de API_Ritweb (eventos RIT: alertas, inspecciones).
/// </summary>
public class ApiRitwebSettings
{
    public const string SectionName = "ApiRitweb";
    public string BaseUrl { get; set; } = "";
}
