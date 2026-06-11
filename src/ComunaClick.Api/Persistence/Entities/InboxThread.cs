namespace ComunaClick.Api.Persistence.Entities;

public sealed class InboxThread
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid? PartnerId { get; set; }
    public Guid? ProfessionalId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Status { get; set; } = "open";
    public DateTimeOffset LastMessageAt { get; set; }
    public DateTimeOffset? CustomerLastReadAt { get; set; }
    public DateTimeOffset? PartnerLastReadAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Customer Customer { get; set; } = null!;
    public Partner? Partner { get; set; }
    public Professional? Professional { get; set; }
    public ICollection<InboxMessage> Messages { get; set; } = new List<InboxMessage>();
}
