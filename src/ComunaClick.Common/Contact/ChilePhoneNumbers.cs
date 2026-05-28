namespace ComunaClick.Common.Contact;

/// <summary>
/// Normalización de teléfonos móviles chilenos para almacenamiento y enlaces WhatsApp.
/// </summary>
public static class ChilePhoneNumbers
{
    public const string StorageHint = "Móvil Chile: +56 9 XXXX XXXX o 9XXXXXXXX (WhatsApp).";

    public static string? NormalizeForStorage(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return null;
        }

        return WhatsAppLinks.TryNormalizeToWaMeDigits(phone.Trim(), out var digits)
            ? digits
            : phone.Trim();
    }

    public static bool IsValidMobile(string? phone)
        => WhatsAppLinks.IsValidMobile(phone);

    public static string? FormatDisplay(string? phone)
    {
        if (!WhatsAppLinks.TryNormalizeToWaMeDigits(phone, out var digits)
            || digits.Length < 11
            || !digits.StartsWith("56", StringComparison.Ordinal))
        {
            return string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        }

        var local = digits[2..];
        if (local.Length == 9)
        {
            return $"+56 {local[0]} {local[1..5]} {local[5..]}";
        }

        return $"+{digits}";
    }
}
