namespace Payments.Gateway.Api.Persistence.Entities;

public sealed class Charge
{
    public Guid Id { get; set; }
    public Guid CustomerTokenId { get; set; }
    public Guid IntentId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "CLP";
    public string Status { get; set; } = "pending";
    public string? ProviderRef { get; set; }
    public string? AuthorizationCode { get; set; }
    public string RawResponse { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; }

    public CustomerToken CustomerToken { get; set; } = null!;
    public PaymentIntent Intent { get; set; } = null!;
}
