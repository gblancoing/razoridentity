namespace ComunaClick.Api.Integrations.Notifications;

/// <summary>
/// Envío de correo desacoplado del armado del mensaje, para poder reintentar desde el
/// worker del outbox y sustituirlo en pruebas. Debe lanzar excepción ante fallo de envío.
/// </summary>
public interface IEmailSender
{
    bool IsConfigured { get; }

    Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default);
}
