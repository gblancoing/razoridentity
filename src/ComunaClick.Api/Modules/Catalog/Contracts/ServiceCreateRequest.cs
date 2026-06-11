namespace ComunaClick.Api.Modules.Catalog.Contracts;

public sealed record ServiceCreateRequest(
    Guid PartnerId,
    string Name,
    string? Description,
    string? Category,
    decimal Price,
    string? Currency,
    int? DurationMinutes,
    bool? IsActive,
    string? ImageUrl,
    string? ServiceAddress,
    Guid? CountryId,
    Guid? RegionId,
    Guid? ComunaId,
    double? Latitude,
    double? Longitude,
    IReadOnlyList<Guid>? ProfessionalIds);
