namespace Payments.Gateway.Api.Persistence.Entities;

public sealed class PaymentIntent
{
    public Guid Id { get; set; }
    public string ExternalReference { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "CLP";
    public string Status { get; set; } = "pending";
    public string Provider { get; set; } = "transbank";
    public string? ProviderToken { get; set; }
    public string? AuthorizationCode { get; set; }
    public string RawResponse { get; set; } = "{}";

    /// <summary>Marca de revisión manual desde el panel: flagged | resolved.</summary>
    public string? ReviewStatus { get; set; }
    public string? ReviewNote { get; set; }

    /// <summary>Última notificación exitosa (o reintentada) hacia el Core.</summary>
    public DateTimeOffset? CoreNotifiedAt { get; set; }
    public int CoreNotifyAttempts { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<ProviderEvent> ProviderEvents { get; set; } = new List<ProviderEvent>();
}
