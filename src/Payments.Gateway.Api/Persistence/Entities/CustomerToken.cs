namespace Payments.Gateway.Api.Persistence.Entities;

public sealed class CustomerToken
{
    public Guid Id { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public string ProviderRef { get; set; } = string.Empty;
    public string Status { get; set; } = "active";
    public string RawResponse { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }

    public ICollection<Charge> Charges { get; set; } = new List<Charge>();
}
