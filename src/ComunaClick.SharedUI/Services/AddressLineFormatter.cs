namespace ComunaClick.SharedUI.Services;

public static class AddressLineFormatter
{
    public static string Combine(string? street, string? number)
    {
        var s = street?.Trim() ?? string.Empty;
        var n = number?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(s))
        {
            return n;
        }

        if (string.IsNullOrEmpty(n))
        {
            return s;
        }

        if (n.StartsWith('#'))
        {
            return $"{s} {n}".Trim();
        }

        return $"{s} #{n}".Trim();
    }

    public static (string Street, string Number) Split(string? full)
    {
        if (string.IsNullOrWhiteSpace(full))
        {
            return (string.Empty, string.Empty);
        }

        var trimmed = full.Trim();
        var hashIdx = trimmed.IndexOf('#');
        if (hashIdx >= 0)
        {
            var street = trimmed[..hashIdx].Trim().TrimEnd(',', '.');
            var number = trimmed[(hashIdx + 1)..].Trim();
            return (street, number);
        }

        return (trimmed, string.Empty);
    }
}
