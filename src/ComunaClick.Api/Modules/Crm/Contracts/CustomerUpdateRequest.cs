namespace ComunaClick.Api.Modules.Crm.Contracts;

public sealed record CustomerUpdateRequest(
    string? Email,
    string? Phone,
    string? FullName,
    string? AvatarUrl = null,
    Guid? CountryId = null,
    Guid? RegionId = null,
    Guid? ComunaId = null,
    string? Address = null,
    double? Latitude = null,
    double? Longitude = null,
    bool UpdateDeliveryAddress = false);
