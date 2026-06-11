namespace ComunaClick.Api.Modules.Search.Contracts;

public sealed record SearchResultItem(
    string Type,
    Guid Id,
    string Name,
    string? Category,
    Guid? PartnerId,
    decimal? Price,
    string? Currency,
    string CtaLabel,
    string CtaHref,
    double? DistanceKm = null,
    double? Latitude = null,
    double? Longitude = null,
    string? LogoUrl = null,
    string? ImageUrl = null,
    IReadOnlyList<string>? ImageUrls = null);
