namespace Payments.App.Services;

public sealed class MercadoPagoAppOptions
{
    public bool Enabled { get; set; } = true;
    public string Environment { get; set; } = "sandbox";
    public string PublicKey { get; set; } = string.Empty;
    public string CreatePreferenceUrl { get; set; } = "https://api.mercadopago.com/checkout/preferences";
    public string GetPaymentUrl { get; set; } = "https://api.mercadopago.com/v1/payments";
    public string WebhookTopic { get; set; } = "payment";
    public bool ValidateWebhookSignature { get; set; }
}
