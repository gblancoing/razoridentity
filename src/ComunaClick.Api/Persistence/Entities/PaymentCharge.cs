namespace ComunaClick.Api.Persistence.Entities;

public sealed class PaymentCharge
{
    public Guid Id { get; set; }
    public Guid CustomerTokenId { get; set; }
    public Guid IntentId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "CLP";
    public string Status { get; set; } = string.Empty;
    public string? ProviderRef { get; set; }
    public string? AuthorizationCode { get; set; }
    public string RawResponse { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; }
}
