namespace ComunaClick.Shared.Api.Partner;

public sealed record SellerFeeSettings(
    decimal FixedFeeAmount,
    decimal PercentageFee,
    bool IsActive);

public sealed record SellerMercadoPagoStatus(
    Guid SellerId,
    string SellerName,
    string ConnectionStatus,
    string? MpUserId,
    string? Scope,
    DateTimeOffset? ConnectedAt,
    DateTimeOffset? TokenExpiresAt,
    SellerFeeSettings FeeSettings);

public sealed record MercadoPagoOAuthStart(
    Guid SellerId,
    string AuthorizationUrl,
    string State);

public sealed record UpdateSellerFees(
    decimal FixedFeeAmount,
    decimal PercentageFee,
    bool IsActive = true);

public sealed record MarketplacePaymentStatusItem(
    string? PreviousStatus,
    string NewStatus,
    string? Detail,
    DateTimeOffset CreatedAt);

public sealed record MarketplacePaymentDetail(
    Guid PaymentId,
    Guid OrderId,
    Guid SellerId,
    string SellerName,
    string BuyerEmail,
    string? BuyerName,
    string Provider,
    string Status,
    string? StatusDetail,
    string Currency,
    double GrossAmount,
    double PlatformFeeAmount,
    double NetAmount,
    double? MercadoPagoFeeAmount,
    double? PaidAmount,
    string ExternalReference,
    string? MercadoPagoPaymentId,
    string? PaymentMethod,
    DateTimeOffset? DateApproved,
    DateTimeOffset CreatedAt,
    IReadOnlyList<MarketplacePaymentStatusItem> History);
