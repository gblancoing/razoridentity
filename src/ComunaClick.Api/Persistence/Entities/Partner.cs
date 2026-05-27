namespace ComunaClick.Api.Persistence.Entities;

public sealed class Partner
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? CountryId { get; set; }
    public Guid? RegionId { get; set; }
    public Guid? ComunaId { get; set; }
    public Guid? SubcategoryId { get; set; }
    public string Type { get; set; } = "A";
    public string Name { get; set; } = string.Empty;
    public string? Rut { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public bool IsVisible { get; set; }
    /// <summary>Comercio tipo A que también publica avisos de servicio.</summary>
    public bool OffersServices { get; set; }
    public string? BannerUrl { get; set; }
    public string? LogoUrl { get; set; }
    public string? StorefrontTagline { get; set; }
    public string? StorefrontAbout { get; set; }
    public string? StorefrontHighlight1 { get; set; }
    public string? StorefrontHighlight2 { get; set; }
    public string? StorefrontHighlight3 { get; set; }
    public string? BankName { get; set; }
    public string? BankAccountType { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? BankAccountHolder { get; set; }
    public string? BankAccountHolderRut { get; set; }
    public string? WebsiteUrl { get; set; }
    public string? InstagramUrl { get; set; }
    public string? FacebookUrl { get; set; }
    public string? LinkedInUrl { get; set; }
    public string? XUrl { get; set; }
    public string? TikTokUrl { get; set; }
    public string? YouTubeUrl { get; set; }
    public string? OtherLinkLabel { get; set; }
    public string? OtherLinkUrl { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public ProductSubcategory? Subcategory { get; set; }
}
