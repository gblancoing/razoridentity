using System.Text.Json;

namespace ComunaClick.Shared.Auth;

public static class JwtHelper
{
    public static string? GetStringClaim(string token, string claim)
    {
        if (!TryGetPayload(token, out var doc) || doc is null)
        {
            return null;
        }

        using (doc)
        {
            if (!doc.RootElement.TryGetProperty(claim, out var element) || element.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            return element.GetString();
        }
    }

    public static Guid? GetGuidClaim(string token, string claim)
    {
        if (!TryGetPayload(token, out var doc) || doc is null)
        {
            return null;
        }

        using (doc)
        {
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
    }

    private static bool TryGetPayload(string token, out JsonDocument? doc)
    {
        doc = null;
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var parts = token.Split('.');
        if (parts.Length < 2)
        {
            return false;
        }

        var json = DecodeBase64Url(parts[1]);
        if (json is null)
        {
            return false;
        }

        try
        {
            doc = JsonDocument.Parse(json);
            return true;
        }
        catch
        {
            return false;
        }
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
