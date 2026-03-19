namespace ComunaClick.Api.Persistence.Entities;

public sealed class Partner
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? SubcategoryId { get; set; }
    public string Type { get; set; } = "A";
    public string Name { get; set; } = string.Empty;
    public string? Rut { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public bool IsVisible { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public ProductSubcategory? Subcategory { get; set; }
}
