namespace ComunaClick.Api.Modules.Marketplace.Contracts;

public sealed record SellerMercadoPagoStatusResponse(
    Guid SellerId,
    string SellerName,
    string ConnectionStatus,
    string? MpUserId,
    string? Scope,
    DateTimeOffset? ConnectedAt,
    DateTimeOffset? TokenExpiresAt,
    SellerFeeSettingsResponse FeeSettings);

public sealed record SellerFeeSettingsResponse(
    decimal FixedFeeAmount,
    decimal PercentageFee,
    bool IsActive);
