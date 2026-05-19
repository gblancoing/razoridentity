namespace ComunaClick.Api.Modules.Catalog.Contracts;

public sealed record ProductCreateRequest(
    Guid PartnerId,
    string Name,
    string? Description,
    string? Category,
    string? ImageUrl,
    decimal Price,
    string? Currency,
    bool? IsActive);
