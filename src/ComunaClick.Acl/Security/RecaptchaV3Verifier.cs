using System.Net.Http.Json;

namespace ComunaClick.Acl.Security;

public sealed class RecaptchaV3Verifier
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public RecaptchaV3Verifier(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public async Task<bool> VerifyAsync(string? token, string expectedAction, string? remoteIp, CancellationToken cancellationToken)
    {
        var enabled = _configuration.GetValue("Recaptcha:Enabled", false);
        if (!enabled)
        {
            return true;
        }

        var secretKey = _configuration["Recaptcha:SecretKey"];
        if (string.IsNullOrWhiteSpace(secretKey) || string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var payload = new Dictionary<string, string>
        {
            ["secret"] = secretKey,
            ["response"] = token
        };

        if (!string.IsNullOrWhiteSpace(remoteIp))
        {
            payload["remoteip"] = remoteIp;
        }

        using var content = new FormUrlEncodedContent(payload);
        using var response = await _httpClient.PostAsync("https://www.google.com/recaptcha/api/siteverify", content, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return false;
        }

        var verification = await response.Content.ReadFromJsonAsync<RecaptchaVerificationResponse>(cancellationToken: cancellationToken);
        if (verification is null || !verification.Success)
        {
            return false;
        }

        var minimumScore = _configuration.GetValue("Recaptcha:MinimumScore", 0.5d);
        if (verification.Score < minimumScore)
        {
            return false;
        }

        if (!string.Equals(verification.Action, expectedAction, StringComparison.Ordinal))
        {
            return false;
        }

        var expectedHostname = _configuration["Recaptcha:ExpectedHostname"];
        if (string.IsNullOrWhiteSpace(expectedHostname))
        {
            return true;
        }

        var hostname = verification.Hostname ?? string.Empty;
        return hostname.Equals(expectedHostname, StringComparison.OrdinalIgnoreCase)
            || hostname.EndsWith("." + expectedHostname, StringComparison.OrdinalIgnoreCase);
    }

    private sealed record RecaptchaVerificationResponse(
        bool Success,
        double Score,
        string? Action,
        string? Hostname
    );
}
