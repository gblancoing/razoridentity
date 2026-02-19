namespace RazorIdentity.Configuration;

/// <summary>
/// Configuración SMTP para envío real de correos (confirmación, recuperación de contraseña).
/// Guarda la contraseña en User Secrets en desarrollo: dotnet user-secrets set "EmailSettings:Password" "tu_password"
/// </summary>
public class EmailSettings
{
    public const string SectionName = "EmailSettings";

    /// <summary>Servidor SMTP (ej. smtp.gmail.com, smtp.office365.com)</summary>
    public string Host { get; set; } = "";

    /// <summary>Puerto (587 para STARTTLS, 465 para SSL)</summary>
    public int Port { get; set; } = 587;

    /// <summary>Usuario SMTP (correo que envía)</summary>
    public string User { get; set; } = "";

    /// <summary>Contraseña o contraseña de aplicación</summary>
    public string Password { get; set; } = "";

    /// <summary>Dirección "De" (ej. noreply@tudominio.com)</summary>
    public string FromEmail { get; set; } = "";

    /// <summary>Nombre que aparece como remitente</summary>
    public string FromName { get; set; } = "RazorIdentity";

    /// <summary>Usar SSL/TLS (true para puerto 465)</summary>
    public bool EnableSsl { get; set; } = true;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(User) && !string.IsNullOrWhiteSpace(Password);
}
