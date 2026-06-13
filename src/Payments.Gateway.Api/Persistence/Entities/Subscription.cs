namespace Payments.Gateway.Api.Persistence.Entities;

/// <summary>
/// Suscripción recurrente real del gateway (reemplaza la heurística "SUBS"
/// sobre external_reference de los payment intents).
/// </summary>
public sealed class Subscription
{
    public Guid Id { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public Guid? CustomerTokenId { get; set; }
    public string ExternalReference { get; set; } = string.Empty;
    public string? PlanName { get; set; }
    public string Provider { get; set; } = "transbank";
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "CLP";
    public string BillingInterval { get; set; } = "monthly";

    /// <summary>active | paused | past_due | cancelled | expired.</summary>
    public string Status { get; set; } = "active";

    public DateTimeOffset? NextChargeAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public CustomerToken? CustomerToken { get; set; }
    public ICollection<SubscriptionAttempt> Attempts { get; set; } = new List<SubscriptionAttempt>();
}
