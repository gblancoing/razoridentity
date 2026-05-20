namespace ComunaClick.Api.Modules.Crm;

internal static class CustomerProfileHelper
{
    /// <summary>URLs https públicas o rutas /uploads servidas por la API; evita data:/javascript:.</summary>
    public static string? SanitizeAvatarUrl(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var t = raw.Trim();
        if (t.Length > 2048)
            return null;
        if (t.Contains('\r') || t.Contains('\n') || t.Contains('<', StringComparison.Ordinal) || t.Contains('"'))
            return null;

        if (t.StartsWith("/uploads/customer-avatars/", StringComparison.OrdinalIgnoreCase))
            return t;

        if (t.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return t;

        if (IsLocalDevHttpUrl(t))
            return t;

        return null;
    }

    private static bool IsLocalDevHttpUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;
        if (!string.Equals(uri.Scheme, "http", StringComparison.OrdinalIgnoreCase))
            return false;

        return uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
               || uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase);
    }
}
