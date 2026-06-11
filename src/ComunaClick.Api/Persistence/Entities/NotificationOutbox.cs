namespace ComunaClick.Api.Persistence.Entities;

/// <summary>
/// Outbox de notificaciones: se persiste dentro de la transacción que genera el evento
/// (orden creada, orden pagada, etc.) y un worker la envía de forma asíncrona con
/// reintentos y backoff, de modo que un SMTP lento o caído no bloquee ni pierda el aviso.
/// </summary>
public sealed class NotificationOutbox
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    /// <summary>"email" | "whatsapp".</summary>
    public string Channel { get; set; } = "email";

    /// <summary>"partner_order_paid" | "buyer_order" | "buyer_booking" | "buyer_payment_pending" | ...</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Id de la orden/reserva relacionada (para idempotencia y trazabilidad).</summary>
    public Guid? ReferenceId { get; set; }

    public string? Recipient { get; set; }
    public string? Subject { get; set; }
    public string Body { get; set; } = string.Empty;

    /// <summary>Datos adicionales para canales no-email (p. ej. payload del webhook de WhatsApp).</summary>
    public string? PayloadJson { get; set; }

    /// <summary>"pending" | "sent" | "failed" (terminal tras agotar reintentos).</summary>
    public string Status { get; set; } = "pending";

    public int Attempts { get; set; }
    public int MaxAttempts { get; set; } = 5;
    public DateTimeOffset NextAttemptAt { get; set; }
    public string? LastError { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? SentAt { get; set; }
}
