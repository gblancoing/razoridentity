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
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<ProviderEvent> ProviderEvents { get; set; } = new List<ProviderEvent>();
}
