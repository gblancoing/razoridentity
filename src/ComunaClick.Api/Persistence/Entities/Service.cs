namespace ComunaClick.Api.Persistence.Entities;

public sealed class Service
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid PartnerId { get; set; }
    public Guid? CountryId { get; set; }
    public Guid? RegionId { get; set; }
    public Guid? ComunaId { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public decimal Price { get; set; }
    public string Currency { get; set; } = "CLP";
    public int DurationMinutes { get; set; } = 30;
    public bool IsBookable { get; set; }
    public bool RequiresOnlinePayment { get; set; }
    public string? ImageUrl { get; set; }
    public string? ServiceAddress { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? DeletedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public IReadOnlyList<Guid> ProfessionalIds { get; set; } = Array.Empty<Guid>();

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public IReadOnlyList<string> ImageUrls { get; set; } = Array.Empty<string>();

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public IReadOnlyList<Guid> ImageIds { get; set; } = Array.Empty<Guid>();

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public IReadOnlyList<ServiceImageSnapshot> Images { get; set; } = Array.Empty<ServiceImageSnapshot>();

    /// <summary>Dirección del partner para mostrar si no hay <see cref="ServiceAddress"/> (solo respuestas públicas).</summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string? PartnerAddress { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string? PartnerName { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string? PartnerLogoUrl { get; set; }
}
