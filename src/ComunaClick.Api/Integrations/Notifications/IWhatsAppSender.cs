namespace ComunaClick.Api.Integrations.Notifications;

/// <summary>
/// Envío de WhatsApp vía webhook configurable. Debe lanzar excepción ante fallo para que
/// el worker del outbox reintente.
/// </summary>
public interface IWhatsAppSender
{
    bool IsConfigured { get; }

    Task SendAsync(string payloadJson, CancellationToken cancellationToken = default);
}
