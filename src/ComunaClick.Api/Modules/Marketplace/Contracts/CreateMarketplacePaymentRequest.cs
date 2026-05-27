using ComunaClick.Api.Modules.Marketplace;

namespace ComunaClick.Api.Modules.Marketplace.Contracts;

public sealed record CreateMarketplacePaymentRequest(
    Guid SellerId,
    Guid? OrderId,
    MarketplaceBuyerRequest Buyer,
    IReadOnlyList<MarketplaceOrderItemRequest> Items,
    MarketplaceFeeOverrideRequest? FeeOverride,
    string? PaymentToken,
    string? PaymentMethodId,
    int? Installments,
    string? IssuerId,
    string? Description,
    string Flow = "checkout_api",
    string? IdempotencyKey = null,
    MercadoPagoBackUrls? BackUrls = null);

public sealed record MarketplaceBuyerRequest(
    string Email,
    string? Name,
    string? IdentificationType = null,
    string? IdentificationNumber = null);

public sealed record MarketplaceOrderItemRequest(
    string? Sku,
    string Title,
    int Quantity,
    decimal UnitPrice);

public sealed record MarketplaceFeeOverrideRequest(
    decimal? FixedFeeAmount,
    decimal? PercentageFee);
