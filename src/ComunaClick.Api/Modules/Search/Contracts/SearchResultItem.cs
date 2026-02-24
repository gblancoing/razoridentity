namespace ComunaClick.Api.Modules.Search.Contracts;

public sealed record SearchResultItem(
    string Type,
    Guid Id,
    string Name,
    string? Category,
    Guid? PartnerId,
    decimal? Price,
    string? Currency);
