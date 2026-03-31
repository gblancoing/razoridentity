namespace ComunaClick.SharedUI.Services;

public static class CategoryVisualService
{
    private static readonly Dictionary<string, string> ImageMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ALIMENTOS"] = "_content/ComunaClick.SharedUI/category-images/alimentos-y-bebidas.png",
        ["food-beverages"] = "_content/ComunaClick.SharedUI/category-images/alimentos-y-bebidas.png",
        ["food-drink"] = "_content/ComunaClick.SharedUI/category-images/alimentos-y-bebidas.png",
        ["home-cleaning"] = "_content/ComunaClick.SharedUI/category-images/hogar-limpieza.png",
        ["health-personal-care"] = "_content/ComunaClick.SharedUI/category-images/salud-y-cuidado.png",
        ["health-care"] = "_content/ComunaClick.SharedUI/category-images/salud-y-cuidado.png",
        ["fashion-accessories"] = "_content/ComunaClick.SharedUI/category-images/moda.png",
        ["tech-accessories"] = "_content/ComunaClick.SharedUI/category-images/tecnologia.png",
        ["gifts-celebrations"] = "_content/ComunaClick.SharedUI/category-images/regalos.png",
        ["sports-outdoors"] = "_content/ComunaClick.SharedUI/category-images/deporte.png",
        ["stationery-office"] = "_content/ComunaClick.SharedUI/category-images/libreria.png",
        ["crafts-entrepreneurs"] = "_content/ComunaClick.SharedUI/category-images/artesania.png"
    };

    public static string? ResolveImage(string? code, string? name)
    {
        if (!string.IsNullOrWhiteSpace(code) && ImageMap.TryGetValue(code.Trim(), out var fromCode))
        {
            return fromCode;
        }

        var normalizedName = Normalize(name);
        return normalizedName switch
        {
            "alimentos y bebidas" => "_content/ComunaClick.SharedUI/category-images/alimentos-y-bebidas.png",
            "alimentos" => "_content/ComunaClick.SharedUI/category-images/alimentos-y-bebidas.png",
            "hogar y limpieza" => "_content/ComunaClick.SharedUI/category-images/hogar-limpieza.png",
            "salud y cuidado personal" => "_content/ComunaClick.SharedUI/category-images/salud-y-cuidado.png",
            "moda y accesorios" => "_content/ComunaClick.SharedUI/category-images/moda.png",
            "tecnologia y accesorios" => "_content/ComunaClick.SharedUI/category-images/tecnologia.png",
            "regalos y celebraciones" => "_content/ComunaClick.SharedUI/category-images/regalos.png",
            "deportes y aire libre" => "_content/ComunaClick.SharedUI/category-images/deporte.png",
            "libreria y oficina" => "_content/ComunaClick.SharedUI/category-images/libreria.png",
            "artesania y emprendimientos" => "_content/ComunaClick.SharedUI/category-images/artesania.png",
            _ => null
        };
    }

    public static string ToSlug(string? code, string? name)
    {
        var source = !string.IsNullOrWhiteSpace(code) ? code! : name ?? string.Empty;
        source = Normalize(source);
        var chars = source
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray();
        var slug = new string(chars);
        while (slug.Contains("--", StringComparison.Ordinal))
        {
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        }

        return slug.Trim('-');
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Trim()
            .ToLowerInvariant()
            .Replace("á", "a", StringComparison.Ordinal)
            .Replace("é", "e", StringComparison.Ordinal)
            .Replace("í", "i", StringComparison.Ordinal)
            .Replace("ó", "o", StringComparison.Ordinal)
            .Replace("ú", "u", StringComparison.Ordinal)
            .Replace("ñ", "n", StringComparison.Ordinal);
    }
}
