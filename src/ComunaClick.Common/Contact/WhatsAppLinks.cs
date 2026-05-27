using System.Text;

namespace ComunaClick.Common.Contact;

/// <summary>
/// Builds wa.me links for handoff from in-app messaging. Does not call the WhatsApp API.
/// </summary>
public static class WhatsAppLinks
{
    private const string DefaultCountryCode = "56";

    public static bool IsValidMobile(string? phone)
        => TryNormalizeToWaMeDigits(phone, out _);

    public static string? TryBuildChatUrl(string? phone, string? prefilledMessage = null)
    {
        if (!TryNormalizeToWaMeDigits(phone, out var digits))
        {
            return null;
        }

        var url = new StringBuilder("https://wa.me/");
        url.Append(digits);
        if (!string.IsNullOrWhiteSpace(prefilledMessage))
        {
            url.Append("?text=");
            url.Append(Uri.EscapeDataString(prefilledMessage.Trim()));
        }

        return url.ToString();
    }

    public static bool CanHandoff(string? ownPhone, string? counterpartyPhone)
        => IsValidMobile(ownPhone) && IsValidMobile(counterpartyPhone);

    public static string BuildThreadHandoffMessage(string? subject, string? counterpartyName, IReadOnlyList<string>? recentBodies = null)
    {
        var lines = new List<string> { "Hola, continúo nuestra conversación desde ComunaClic." };
        if (!string.IsNullOrWhiteSpace(subject))
        {
            lines.Add($"Asunto: {subject.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(counterpartyName))
        {
            lines.Add($"Con: {counterpartyName.Trim()}");
        }

        if (recentBodies is { Count: > 0 })
        {
            lines.Add("");
            lines.Add("Últimos mensajes:");
            foreach (var body in recentBodies.TakeLast(3))
            {
                if (string.IsNullOrWhiteSpace(body))
                {
                    continue;
                }

                var snippet = body.Trim();
                if (snippet.Length > 280)
                {
                    snippet = snippet[..277] + "…";
                }

                lines.Add($"• {snippet}");
            }
        }

        return string.Join("\n", lines);
    }

    public static bool TryNormalizeToWaMeDigits(string? phone, out string digits)
    {
        digits = string.Empty;
        if (string.IsNullOrWhiteSpace(phone))
        {
            return false;
        }

        var raw = new string(phone.Where(char.IsDigit).ToArray());
        if (raw.Length < 8)
        {
            return false;
        }

        if (raw.StartsWith("00", StringComparison.Ordinal))
        {
            raw = raw[2..];
        }

        if (raw.StartsWith(DefaultCountryCode, StringComparison.Ordinal) && raw.Length >= 11)
        {
            digits = raw;
            return IsPlausibleChileMobile(digits);
        }

        if (raw.StartsWith('9') && raw.Length is >= 8 and <= 9)
        {
            digits = DefaultCountryCode + raw;
            return IsPlausibleChileMobile(digits);
        }

        if (raw.Length >= 10 && raw.Length <= 15)
        {
            digits = raw;
            return true;
        }

        return false;
    }

    private static bool IsPlausibleChileMobile(string digitsWithCountry)
    {
        if (!digitsWithCountry.StartsWith(DefaultCountryCode, StringComparison.Ordinal))
        {
            return digitsWithCountry.Length >= 10;
        }

        if (digitsWithCountry.Length < 11)
        {
            return false;
        }

        return digitsWithCountry[2] == '9';
    }
}
