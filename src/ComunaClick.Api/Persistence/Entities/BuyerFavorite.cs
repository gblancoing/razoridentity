namespace ComunaClick.Api.Persistence.Entities;

public sealed class BuyerFavorite
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid CustomerId { get; set; }
    public string Type { get; set; } = string.Empty;
    public Guid TargetId { get; set; }
    public Guid? PartnerId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
