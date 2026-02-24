using System.Text.Json;

namespace ComunaClick.Shared.Auth;

public static class JwtHelper
{
    public static Guid? GetGuidClaim(string token, string claim)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var parts = token.Split('.');
        if (parts.Length < 2)
        {
            return null;
        }

        var payload = parts[1];
        var json = DecodeBase64Url(payload);
        if (json is null)
        {
            return null;
        }

        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty(claim, out var element))
        {
            return null;
        }

        if (element.ValueKind == JsonValueKind.String && Guid.TryParse(element.GetString(), out var guid))
        {
            return guid;
        }

        return null;
    }

    private static string? DecodeBase64Url(string input)
    {
        var padded = input.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2:
                padded += "==";
                break;
            case 3:
                padded += "=";
                break;
        }

        try
        {
            var bytes = Convert.FromBase64String(padded);
            return System.Text.Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return null;
        }
    }
}
