namespace Payments.Gateway.Api.Persistence.Entities;

/// <summary>Intento de cobro de una suscripción (trazable a intent y charge).</summary>
public sealed class SubscriptionAttempt
{
    public Guid Id { get; set; }
    public Guid SubscriptionId { get; set; }
    public Guid? IntentId { get; set; }
    public Guid? ChargeId { get; set; }

    /// <summary>approved | rejected | error.</summary>
    public string Status { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }
    public DateTimeOffset AttemptedAt { get; set; }

    public Subscription Subscription { get; set; } = null!;
}
