namespace Payments.Gateway.Api.Services.Providers;

public sealed class TransbankProviderOptions
{
    public bool Simulate { get; set; } = true;
    public string CreateTransactionUrl { get; set; } = "https://webpay3gint.transbank.cl/rswebpaytransaction/api/webpay/v1.2/transactions";
    public string CommerceCode { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string RedirectBaseUrl { get; set; } = string.Empty;
}

public sealed class KhipuProviderOptions
{
    public bool Simulate { get; set; } = true;
    public string CreatePaymentUrl { get; set; } = "https://payment-api.khipu.com/v3/payments";
    public string ApiKey { get; set; } = string.Empty;
    public string? NotifyUrl { get; set; }
}

public sealed class MercadoPagoProviderOptions
{
    public bool Simulate { get; set; } = true;
    public string CreatePreferenceUrl { get; set; } = "https://api.mercadopago.com/checkout/preferences";
    public string AccessToken { get; set; } = string.Empty;
    public string? PlatformId { get; set; }
}
