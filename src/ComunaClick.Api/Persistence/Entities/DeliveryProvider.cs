namespace ComunaClick.Api.Persistence.Entities;

public sealed class DeliveryProvider
{
    public Guid Id { get; set; }
    public Guid? TenantId { get; set; }
    public Guid? RegionId { get; set; }
    public Guid? ComunaId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ContactName { get; set; }
    public string? ContactPhone { get; set; }
    public string? ContactEmail { get; set; }
    public decimal BaseFee { get; set; }
    public int? EstimatedMinutes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
