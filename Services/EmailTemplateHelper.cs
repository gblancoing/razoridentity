using System.Text.Encodings.Web;

namespace RazorIdentity.Services;

/// <summary>
/// Genera el HTML de correos con formato profesional (confirmación, recuperación de contraseña).
/// </summary>
public static class EmailTemplateHelper
{
    private const string BrandColor = "#FF671B";
    private const string BrandName = "RazorIdentity";

    /// <summary>
    /// Cuerpo del correo con cabecera, texto, botón CTA y pie.
    /// </summary>
    public static string BuildEmailHtml(string titulo, string textoPrincipal, string textoBoton, string urlBoton)
    {
        var urlSegura = HtmlEncoder.Default.Encode(urlBoton);
        return $@"
<!DOCTYPE html>
<html lang=""es"">
<head>
  <meta charset=""utf-8"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
  <title>{HtmlEncoder.Default.Encode(titulo)}</title>
</head>
<body style=""margin:0; padding:0; font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background-color:#f3f4f6; -webkit-font-smoothing:antialiased;"">
  <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background-color:#f3f4f6; padding: 40px 20px;"">
    <tr>
      <td align=""center"">
        <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""max-width: 520px; background-color:#ffffff; border-radius: 12px; box-shadow: 0 4px 6px rgba(0,0,0,0.07); overflow: hidden;"">
          <!-- Cabecera -->
          <tr>
            <td style=""background: linear-gradient(135deg, {BrandColor} 0%, #E55A16 100%); padding: 28px 32px; text-align: center;"">
              <h1 style=""margin:0; color:#ffffff; font-size: 22px; font-weight: 700; letter-spacing: -0.02em;"">{BrandName}</h1>
              <p style=""margin: 6px 0 0 0; color: rgba(255,255,255,0.9); font-size: 13px;"">Módulo de Control de Usuarios</p>
            </td>
          </tr>
          <!-- Contenido -->
          <tr>
            <td style=""padding: 32px 32px 24px;"">
              <h2 style=""margin: 0 0 16px 0; color: #1f2937; font-size: 18px; font-weight: 600;"">{HtmlEncoder.Default.Encode(titulo)}</h2>
              <p style=""margin: 0 0 24px 0; color: #4b5563; font-size: 15px; line-height: 1.6;"">{HtmlEncoder.Default.Encode(textoPrincipal)}</p>
              <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" style=""margin: 0 auto;"">
                <tr>
                  <td style=""border-radius: 8px; background-color: {BrandColor};"">
                    <a href=""{urlSegura}"" target=""_blank"" style=""display: inline-block; padding: 14px 28px; color: #ffffff; font-size: 14px; font-weight: 700; text-decoration: none; letter-spacing: 0.02em;"">{HtmlEncoder.Default.Encode(textoBoton)}</a>
                  </td>
                </tr>
              </table>
              <p style=""margin: 20px 0 0 0; color: #9ca3af; font-size: 12px; line-height: 1.5;"">Si el botón no funciona, copie y pegue este enlace en su navegador:<br><a href=""{urlSegura}"" style=""color: {BrandColor}; word-break: break-all;"">{urlSegura}</a></p>
            </td>
          </tr>
          <!-- Pie -->
          <tr>
            <td style=""padding: 20px 32px; background-color: #f9fafb; border-top: 1px solid #e5e7eb;"">
              <p style=""margin: 0; color: #6b7280; font-size: 12px; text-align: center;"">© {DateTime.UtcNow.Year} {BrandName}. Todos los derechos reservados.</p>
            </td>
          </tr>
        </table>
      </td>
    </tr>
  </table>
</body>
</html>";
    }

    /// <summary>
    /// HTML para el correo de confirmación de cuenta (registro / reenviar).
    /// </summary>
    public static string BuildConfirmacionCorreoHtml(string confirmarUrl)
    {
        return BuildEmailHtml(
            titulo: "Confirmar su correo electrónico",
            textoPrincipal: "Gracias por registrarse. Para activar su cuenta y poder iniciar sesión, haga clic en el botón de abajo.",
            textoBoton: "Confirmar mi cuenta",
            urlBoton: confirmarUrl);
    }

    /// <summary>
    /// HTML para el correo de recuperación de contraseña.
    /// </summary>
    public static string BuildRecuperarPasswordHtml(string restablecerUrl)
    {
        return BuildEmailHtml(
            titulo: "Recuperar contraseña",
            textoPrincipal: "Ha solicitado restablecer la contraseña de su cuenta. Haga clic en el botón para elegir una nueva contraseña. El enlace es válido por tiempo limitado.",
            textoBoton: "Restablecer contraseña",
            urlBoton: restablecerUrl);
    }
}
