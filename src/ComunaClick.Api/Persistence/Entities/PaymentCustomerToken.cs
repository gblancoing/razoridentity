namespace ComunaClick.Api.Persistence.Entities;

public sealed class PaymentCustomerToken
{
    public Guid Id { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public string ProviderRef { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string RawResponse { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
}
