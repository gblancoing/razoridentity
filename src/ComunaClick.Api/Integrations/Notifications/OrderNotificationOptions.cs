namespace ComunaClick.Api.Integrations.Notifications;

public sealed class OrderNotificationOptions
{
    public bool Enabled { get; set; } = false;
    public bool EnableEmail { get; set; } = false;
    public bool EnableBuyerEmail { get; set; } = true;
    public string AppBaseUrl { get; set; } = "https://app.comunaclic.cl";
    public bool EnableWhatsAppWebhook { get; set; } = false;
    public string? WhatsAppWebhookUrl { get; set; }
    public string? WhatsAppApiKey { get; set; }
    public SmtpOptions Smtp { get; set; } = new();
}

public sealed class SmtpOptions
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
}
