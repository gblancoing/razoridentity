using Microsoft.AspNetCore.Identity.UI.Services;

namespace RazorIdentity.Services;

/// <summary>
/// Implementación de IEmailSender que registra el contenido en consola.
/// Útil para desarrollo. En producción reemplazar por un envío real (SMTP, SendGrid, etc.).
/// </summary>
public class LoggingEmailSender : IEmailSender
{
    private readonly ILogger<LoggingEmailSender> _logger;

    public LoggingEmailSender(ILogger<LoggingEmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        _logger.LogInformation("Email para {Email}, asunto: {Subject}. Contenido (enlace): {Message}",
            email, subject, htmlMessage);
        return Task.CompletedTask;
    }
}
