namespace ComunaClick.Admin.Services;

public sealed class PaymentGatewayOptions
{
    public string BaseUrl { get; set; } = "https://payments.comunaclic.cl";
    public bool UseMockFallback { get; set; }
}
