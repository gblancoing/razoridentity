namespace ComunaClick.Api.Modules.Marketplace;

public sealed class MercadoPagoMarketplaceOptions
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
    public string ApiBaseUrl { get; set; } = "https://api.mercadopago.com";
    public string OAuthAuthorizeUrl { get; set; } = "https://auth.mercadopago.com/authorization";
    public string WebhookSecret { get; set; } = string.Empty;
    public string AppBaseUrl { get; set; } = "https://app.comunaclic.cl";
    public string EncryptionKey { get; set; } = string.Empty;
    public string Currency { get; set; } = "CLP";
}
