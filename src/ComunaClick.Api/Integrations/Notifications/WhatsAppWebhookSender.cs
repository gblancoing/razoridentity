using System.Net.Http.Headers;
using Microsoft.Extensions.Options;

namespace ComunaClick.Api.Integrations.Notifications;

public sealed class WhatsAppWebhookSender : IWhatsAppSender
{
    private readonly OrderNotificationOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;

    public WhatsAppWebhookSender(IOptions<OrderNotificationOptions> options, IHttpClientFactory httpClientFactory)
    {
        _options = options.Value;
        _httpClientFactory = httpClientFactory;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.WhatsAppWebhookUrl);

    public async Task SendAsync(string payloadJson, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("WhatsApp webhook no está configurado.");
        }

        var client = _httpClientFactory.CreateClient(nameof(OrderNotificationService));
        using var request = new HttpRequestMessage(HttpMethod.Post, _options.WhatsAppWebhookUrl)
        {
            Content = new StringContent(payloadJson, System.Text.Encoding.UTF8, "application/json")
        };

        if (!string.IsNullOrWhiteSpace(_options.WhatsAppApiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.WhatsAppApiKey);
        }

        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"WhatsApp webhook respondió {(int)response.StatusCode}.");
        }
    }
}
