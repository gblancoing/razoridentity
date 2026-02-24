using System.Net.Http.Json;
using Payments.Gateway.Api.Persistence.Entities;

namespace Payments.Gateway.Api.Services;

public sealed class ComunaClicNotifier : IComunaClicNotifier
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public ComunaClicNotifier(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public async Task NotifyPaymentAsync(PaymentIntent intent, string status, string providerEventId, string? rawPayload, CancellationToken cancellationToken)
    {
        var url = _configuration["ComunaClic:InternalWebhookUrl"];
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        var payload = new
        {
            paymentId = (Guid?)null,
            externalReference = intent.ExternalReference,
            providerEventId,
            status,
            payload = rawPayload
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(payload)
        };

        var key = _configuration["ComunaClic:InternalWebhookKey"];
        if (!string.IsNullOrWhiteSpace(key))
        {
            request.Headers.Add("X-Internal-Key", key);
        }

        await _httpClient.SendAsync(request, cancellationToken);
    }
}
