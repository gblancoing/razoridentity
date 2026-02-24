namespace ComunaClick.Api.Modules.Catalog.Contracts;

public sealed record ProductCreateRequest(
    Guid PartnerId,
    string Name,
    string? Description,
    string? Category,
    decimal Price,
    string? Currency,
    bool? IsActive);
