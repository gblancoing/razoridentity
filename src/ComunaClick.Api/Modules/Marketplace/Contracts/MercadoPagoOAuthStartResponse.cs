namespace ComunaClick.Api.Modules.Marketplace.Contracts;

public sealed record MercadoPagoOAuthStartResponse(
    Guid SellerId,
    string AuthorizationUrl,
    string State);
