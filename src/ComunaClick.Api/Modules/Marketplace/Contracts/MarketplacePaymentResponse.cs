namespace ComunaClick.Api.Modules.Marketplace.Contracts;

public sealed record MarketplacePaymentResponse(
    Guid PaymentId,
    Guid OrderId,
    Guid SellerId,
    string Flow,
    string Provider,
    string Status,
    string? StatusDetail,
    string Currency,
    decimal GrossAmount,
    decimal PlatformFeeAmount,
    decimal NetAmount,
    string ExternalReference,
    string? MercadoPagoPaymentId,
    string? CheckoutUrl,
    string CorrelationId);

public sealed record MarketplacePaymentDetailResponse(
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
    decimal GrossAmount,
    decimal PlatformFeeAmount,
    decimal NetAmount,
    decimal? MercadoPagoFeeAmount,
    decimal? PaidAmount,
    string ExternalReference,
    string? MercadoPagoPaymentId,
    string? PaymentMethod,
    DateTimeOffset? DateApproved,
    DateTimeOffset CreatedAt,
    IReadOnlyList<MarketplacePaymentStatusItemResponse> History);

public sealed record MarketplacePaymentStatusItemResponse(
    string? PreviousStatus,
    string NewStatus,
    string? Detail,
    DateTimeOffset CreatedAt);
