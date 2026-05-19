namespace ComunaClick.Api.Modules.Crm.Contracts;

public sealed record BuyerFavoriteCreateRequest(
    string? Type,
    Guid TargetId,
    Guid? TenantId
);
