namespace ComunaClick.Api.Modules.Crm;

internal static class CustomerProfileHelper
{
    /// <summary>Solo URLs https públicas; evita data:/javascript: y binarios en BD.</summary>
    public static string? SanitizeAvatarUrl(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var t = raw.Trim();
        if (t.Length > 2048)
            return null;
        if (!t.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return null;
        if (t.Contains('\r') || t.Contains('\n') || t.Contains('<', StringComparison.Ordinal) || t.Contains('"'))
            return null;

        return t;
    }
}
