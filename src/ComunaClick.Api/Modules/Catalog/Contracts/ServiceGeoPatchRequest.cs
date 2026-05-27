namespace ComunaClick.Api.Modules.Catalog.Contracts;

public sealed record ServiceGeoPatchRequest(
    Guid? CountryId,
    Guid? RegionId,
    Guid? ComunaId,
    double Latitude,
    double Longitude);
