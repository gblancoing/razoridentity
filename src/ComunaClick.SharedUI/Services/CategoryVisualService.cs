namespace ComunaClick.SharedUI.Services;

public static class CategoryVisualService
{
    private static readonly Dictionary<string, string> ImageMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ALIMENTOS"] = "_content/ComunaClick.SharedUI/category-images/alimentos-y-bebidas.png",
        ["food-beverages"] = "_content/ComunaClick.SharedUI/category-images/alimentos-y-bebidas.png",
        ["food-drink"] = "_content/ComunaClick.SharedUI/category-images/alimentos-y-bebidas.png",
        ["home-cleaning"] = "_content/ComunaClick.SharedUI/category-images/hogar-limpieza.png",
        ["babies-kids"] = "_content/ComunaClick.SharedUI/category-images/regalos.png",
        ["health-personal-care"] = "_content/ComunaClick.SharedUI/category-images/salud-y-cuidado.png",
        ["health-care"] = "_content/ComunaClick.SharedUI/category-images/salud-y-cuidado.png",
        ["fashion-accessories"] = "_content/ComunaClick.SharedUI/category-images/moda.png",
        ["tech-accessories"] = "_content/ComunaClick.SharedUI/category-images/tecnologia.png",
        ["gifts-celebrations"] = "_content/ComunaClick.SharedUI/category-images/regalos.png",
        ["sports-outdoors"] = "_content/ComunaClick.SharedUI/category-images/deporte.png",
        ["stationery-office"] = "_content/ComunaClick.SharedUI/category-images/libreria.png",
        ["crafts-entrepreneurs"] = "_content/ComunaClick.SharedUI/category-images/artesania.png"
    };

    private static readonly Dictionary<string, string> DisplayNameMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["babies-kids"] = "Bebés y Niños",
        ["food-beverages"] = "Alimentos y Bebidas",
        ["food-drink"] = "Alimentos y Bebidas",
        ["home-cleaning"] = "Hogar y Limpieza",
        ["health-personal-care"] = "Salud y Cuidado Personal",
        ["health-care"] = "Salud y Cuidado Personal",
        ["fashion-accessories"] = "Moda y Accesorios",
        ["tech-accessories"] = "Tecnología y Accesorios",
        ["gifts-celebrations"] = "Regalos y Celebraciones",
        ["sports-outdoors"] = "Deportes y Aire Libre",
        ["stationery-office"] = "Librería y Oficina",
        ["crafts-entrepreneurs"] = "Artesanía y Emprendimientos",
        ["ALIMENTOS"] = "Alimentos y Bebidas"
    };

    private static readonly string[] FallbackImages =
    [
        "_content/ComunaClick.SharedUI/category-images/alimentos-y-bebidas.png",
        "_content/ComunaClick.SharedUI/category-images/hogar-limpieza.png",
        "_content/ComunaClick.SharedUI/category-images/salud-y-cuidado.png",
        "_content/ComunaClick.SharedUI/category-images/moda.png",
        "_content/ComunaClick.SharedUI/category-images/tecnologia.png",
        "_content/ComunaClick.SharedUI/category-images/regalos.png",
        "_content/ComunaClick.SharedUI/category-images/deporte.png",
        "_content/ComunaClick.SharedUI/category-images/libreria.png",
        "_content/ComunaClick.SharedUI/category-images/artesania.png"
    ];

    public static string? ResolveImage(string? code, string? name)
    {
        if (TryResolveImageFromMap(code, out var fromCode))
        {
            return fromCode;
        }

        if (TryResolveImageFromMap(name, out var fromName))
        {
            return fromName;
        }

        var normalizedName = Normalize(name);
        var fromKeywords = ResolveImageByKeywords(normalizedName);
        if (!string.IsNullOrWhiteSpace(fromKeywords))
        {
            return fromKeywords;
        }

        return normalizedName switch
        {
            "alimentos y bebidas" => "_content/ComunaClick.SharedUI/category-images/alimentos-y-bebidas.png",
            "alimentos" => "_content/ComunaClick.SharedUI/category-images/alimentos-y-bebidas.png",
            "bebes y ninos" => "_content/ComunaClick.SharedUI/category-images/regalos.png",
            "hogar y limpieza" => "_content/ComunaClick.SharedUI/category-images/hogar-limpieza.png",
            "salud y cuidado personal" => "_content/ComunaClick.SharedUI/category-images/salud-y-cuidado.png",
            "moda y accesorios" => "_content/ComunaClick.SharedUI/category-images/moda.png",
            "tecnologia y accesorios" => "_content/ComunaClick.SharedUI/category-images/tecnologia.png",
            "regalos y celebraciones" => "_content/ComunaClick.SharedUI/category-images/regalos.png",
            "deportes y aire libre" => "_content/ComunaClick.SharedUI/category-images/deporte.png",
            "libreria y oficina" => "_content/ComunaClick.SharedUI/category-images/libreria.png",
            "artesania y emprendimientos" => "_content/ComunaClick.SharedUI/category-images/artesania.png",
            _ => ResolveFallbackImage(code, name)
        };
    }

    public static string ResolveDisplayName(string? code, string? name)
    {
        if (TryResolveDisplayNameFromMap(code, out var mappedName))
        {
            return mappedName;
        }

        if (!string.IsNullOrWhiteSpace(name) && !LooksLikeTechnicalSlug(name))
        {
            return name.Trim();
        }

        if (TryResolveDisplayNameFromMap(name, out var mappedByName))
        {
            return mappedByName;
        }

        var source = !string.IsNullOrWhiteSpace(code) ? code!.Trim() : name?.Trim() ?? "Categoría local";
        return string.Join(' ', source
            .Split(new[] { '-', '_', '/' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(ToTitleWord));
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

    private static string ResolveFallbackImage(string? code, string? name)
    {
        var source = Normalize(!string.IsNullOrWhiteSpace(code) ? code : name);
        if (string.IsNullOrWhiteSpace(source))
        {
            return FallbackImages[0];
        }

        var index = StableIndex(source, FallbackImages.Length);
        return FallbackImages[index];
    }

    private static bool TryResolveImageFromMap(string? value, out string image)
    {
        image = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var raw = value.Trim();
        if (ImageMap.TryGetValue(raw, out image))
        {
            return true;
        }

        var canonical = Canonicalize(raw);
        if (ImageMap.TryGetValue(canonical, out image))
        {
            return true;
        }

        return false;
    }

    private static bool TryResolveDisplayNameFromMap(string? value, out string displayName)
    {
        displayName = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var raw = value.Trim();
        if (DisplayNameMap.TryGetValue(raw, out displayName))
        {
            return true;
        }

        var canonical = Canonicalize(raw);
        if (DisplayNameMap.TryGetValue(canonical, out displayName))
        {
            return true;
        }

        return false;
    }

    private static string? ResolveImageByKeywords(string normalized)
    {
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return normalized switch
        {
            var s when s.Contains("alimento", StringComparison.Ordinal) || s.Contains("bebida", StringComparison.Ordinal) || s.Contains("comida", StringComparison.Ordinal)
                => "_content/ComunaClick.SharedUI/category-images/alimentos-y-bebidas.png",
            var s when s.Contains("hogar", StringComparison.Ordinal) || s.Contains("limpieza", StringComparison.Ordinal)
                => "_content/ComunaClick.SharedUI/category-images/hogar-limpieza.png",
            var s when s.Contains("salud", StringComparison.Ordinal) || s.Contains("cuidado", StringComparison.Ordinal) || s.Contains("wellness", StringComparison.Ordinal)
                => "_content/ComunaClick.SharedUI/category-images/salud-y-cuidado.png",
            var s when s.Contains("moda", StringComparison.Ordinal) || s.Contains("fashion", StringComparison.Ordinal)
                => "_content/ComunaClick.SharedUI/category-images/moda.png",
            var s when s.Contains("tecnologia", StringComparison.Ordinal) || s.Contains("tech", StringComparison.Ordinal)
                => "_content/ComunaClick.SharedUI/category-images/tecnologia.png",
            var s when s.Contains("regalo", StringComparison.Ordinal) || s.Contains("celebr", StringComparison.Ordinal) || s.Contains("bebe", StringComparison.Ordinal) || s.Contains("nino", StringComparison.Ordinal)
                => "_content/ComunaClick.SharedUI/category-images/regalos.png",
            var s when s.Contains("deporte", StringComparison.Ordinal) || s.Contains("sport", StringComparison.Ordinal) || s.Contains("aire libre", StringComparison.Ordinal)
                => "_content/ComunaClick.SharedUI/category-images/deporte.png",
            var s when s.Contains("libreria", StringComparison.Ordinal) || s.Contains("oficina", StringComparison.Ordinal) || s.Contains("papeleria", StringComparison.Ordinal)
                => "_content/ComunaClick.SharedUI/category-images/libreria.png",
            var s when s.Contains("artesania", StringComparison.Ordinal) || s.Contains("emprend", StringComparison.Ordinal) || s.Contains("craft", StringComparison.Ordinal)
                => "_content/ComunaClick.SharedUI/category-images/artesania.png",
            _ => null
        };
    }

    private static string Canonicalize(string value)
    {
        var normalized = Normalize(value);
        var chars = normalized
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray();
        var canonical = new string(chars);
        while (canonical.Contains("--", StringComparison.Ordinal))
        {
            canonical = canonical.Replace("--", "-", StringComparison.Ordinal);
        }

        return canonical.Trim('-');
    }

    private static int StableIndex(string source, int size)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (var c in source)
            {
                hash ^= c;
                hash *= 16777619;
            }

            return (int)(hash % (uint)size);
        }
    }

    private static bool LooksLikeTechnicalSlug(string value)
    {
        return value.Any(ch => ch is '-' or '_') && value.All(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' or '/');
    }

    private static string ToTitleWord(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        value = value.Trim();
        return value.Length == 1
            ? value.ToUpperInvariant()
            : $"{char.ToUpperInvariant(value[0])}{value[1..].ToLowerInvariant()}";
    }
}
