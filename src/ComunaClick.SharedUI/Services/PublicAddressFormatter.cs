using System.Text.RegularExpressions;

namespace ComunaClick.SharedUI.Services;

public static class PublicAddressFormatter
{
    public static string Summarize(string? address, int maxParts = 3)
    {
        var parts = ExtractMeaningfulParts(address);
        if (parts.Count == 0)
        {
            return address?.Trim() ?? string.Empty;
        }

        return string.Join(" · ", parts.Take(Math.Max(1, maxParts)));
    }

    public static (string Primary, string? Secondary) SplitForDisplay(string? address)
    {
        var parts = ExtractMeaningfulParts(address);
        if (parts.Count == 0)
        {
            var fallback = address?.Trim() ?? string.Empty;
            return (fallback, null);
        }

        if (parts.Count == 1)
        {
            return (parts[0], null);
        }

        var primary = parts[0];
        var secondary = parts.Count == 2
            ? parts[1]
            : string.Join(" · ", parts.Skip(1).Take(2));

        return (primary, secondary);
    }

    private static List<string> ExtractMeaningfulParts(string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return [];
        }

        var cleaned = Regex.Replace(address.Trim(), @"\s*#\s*0?\s*$", string.Empty).Trim();

        return cleaned
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(part => !IsAdministrativeNoise(part))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsAdministrativeNoise(string part)
    {
        if (string.IsNullOrWhiteSpace(part))
        {
            return true;
        }

        var lower = part.Trim().ToLowerInvariant();
        if (lower is "chile" or "cl")
        {
            return true;
        }

        if (lower.StartsWith("región ", StringComparison.Ordinal)
            || lower.StartsWith("region ", StringComparison.Ordinal))
        {
            return true;
        }

        if (lower.StartsWith("provincia ", StringComparison.Ordinal))
        {
            return true;
        }

        if (lower.Contains("metropolitan", StringComparison.Ordinal)
            || lower.Contains("metropolitana", StringComparison.Ordinal))
        {
            return true;
        }

        return false;
    }
}
