namespace ComunaClick.Api.Persistence.Entities;

public sealed class SubscriptionPlan
{
    public Guid Id { get; set; }
    public Guid? TenantId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal MonthlyPrice { get; set; }
    public string Currency { get; set; } = "CLP";
    public decimal CommissionPct { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
}
