namespace ComunaClick.Api.Persistence.Entities;

public sealed class Payment
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? SellerId { get; set; }
    public Guid? OrderId { get; set; }
    public string Provider { get; set; } = "transbank";
    public string ExternalReference { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal? TransactionAmount { get; set; }
    public decimal? PaidAmount { get; set; }
    public string Currency { get; set; } = "CLP";
    public string Status { get; set; } = "pending";
    public string? StatusDetail { get; set; }
    public Guid? GatewayIntentId { get; set; }
    public string? ProviderToken { get; set; }
    public string? MercadoPagoPaymentId { get; set; }
    public string? PaymentMethod { get; set; }
    public string? LastEventId { get; set; }
    public string? RawResponseJson { get; set; }
    public string? IdempotencyKey { get; set; }
    public string? CorrelationId { get; set; }
    public DateTimeOffset? DateApproved { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
