namespace ComunaClick.Api.Persistence.Entities;

public sealed class CustomerPartnerLink
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid PartnerId { get; set; }
    public DateTimeOffset FirstSeenAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
    public string? Source { get; set; }
}
