namespace RazorIdentity.Configuration;

/// <summary>URL base del host financiero (Planning &amp; Control): API PHP o futura API .NET.</summary>
public class PycApiSettings
{
    public const string SectionName = "PycApi";
    public string BaseUrl { get; set; } = "";
    /// <summary>
    /// Si es <c>true</c>, delega lectura/importación en <see cref="BaseUrl"/> (API PHP/MySQL legada).
    /// Si es <c>false</c> (predeterminado), la página PMO usa Entity Framework y PostgreSQL en esta aplicación.
    /// </summary>
    public bool UsePhpEndpoints { get; set; } = false;
}
