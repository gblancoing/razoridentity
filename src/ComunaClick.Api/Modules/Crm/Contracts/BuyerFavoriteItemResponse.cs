namespace ComunaClick.Api.Modules.Crm.Contracts;

public sealed record BuyerFavoriteItemResponse(
    Guid Id,
    string Type,
    Guid TargetId,
    Guid CustomerId,
    Guid? PartnerId,
    string? Name,
    string? Summary,
    string? Category,
    decimal? Price,
    string? Currency,
    string? ImageUrl,
    bool IsAvailable,
    DateTimeOffset CreatedAt
);
