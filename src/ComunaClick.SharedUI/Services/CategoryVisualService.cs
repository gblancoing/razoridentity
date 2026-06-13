using ComunaClick.SharedUI.Data;

namespace ComunaClick.SharedUI.Services;

/// <summary>Contexto de imagen: comercio (catálogo retail), servicios B2B locales, o profesionales independientes.</summary>
public enum CategoryVisualKind
{
    Commerce,
    ServiceBusiness,
    Professional
}

public static class CategoryVisualService
{
    private const string ImgAlimentos = "_content/ComunaClick.SharedUI/category-images/alimentos-y-bebidas.png";
    private const string ImgHogar = "_content/ComunaClick.SharedUI/category-images/hogar-limpieza.png";
    private const string ImgSalud = "_content/ComunaClick.SharedUI/category-images/salud-y-cuidado.png";
    private const string ImgModa = "_content/ComunaClick.SharedUI/category-images/moda.png";
    private const string ImgTecnologia = "_content/ComunaClick.SharedUI/category-images/tecnologia.png";
    private const string ImgRegalos = "_content/ComunaClick.SharedUI/category-images/regalos.png";
    private const string ImgDeporte = "_content/ComunaClick.SharedUI/category-images/deporte.png";
    private const string ImgLibreria = "_content/ComunaClick.SharedUI/category-images/libreria.png";
    private const string ImgArtesania = "_content/ComunaClick.SharedUI/category-images/artesania.png";

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
        ["crafts-entrepreneurs"] = "_content/ComunaClick.SharedUI/category-images/artesania.png",
        ["inmuebles"] = "_content/ComunaClick.SharedUI/category-images/hogar-limpieza.png",
        ["real-estate"] = "_content/ComunaClick.SharedUI/category-images/hogar-limpieza.png",
        ["home-improvement-gardening"] = "_content/ComunaClick.SharedUI/category-images/hogar-limpieza.png"
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
        ["ALIMENTOS"] = "Alimentos y Bebidas",
        ["inmuebles"] = "Inmuebles",
        ["real-estate"] = "Inmuebles",
        ["home-improvement-gardening"] = "Mejoramiento del hogar y jardinería"
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

    /// <summary>
    /// Imagen para un ítem del hub de Servicios/Profesionales: misma lógica que <see cref="ResolveImage(string?, string?)"/> (comercio)
    /// usando <see cref="DiscoveryHubItem.CommerceImageKey"/> o <see cref="DiscoveryHubItem.CategoryCode"/> como clave del <c>ImageMap</c>.
    /// </summary>
    public static string? ResolveHubItemImage(DiscoveryHubItem item, bool isProfessional)
    {
        if (!string.IsNullOrWhiteSpace(item.ImageUrl))
        {
            return item.ImageUrl.Trim();
        }

        var key = !string.IsNullOrWhiteSpace(item.CommerceImageKey)
            ? item.CommerceImageKey.Trim()
            : item.CategoryCode?.Trim();
        if (!string.IsNullOrWhiteSpace(key))
        {
            return ResolveCommerceImage(key, item.Title);
        }

        return isProfessional
            ? ResolveProfessionalImage(item.Slug, item.Title)
            : ResolveServiceBusinessImage(item.Slug, item.Title);
    }

    /// <summary>Comercio / categorías de producto (tienda, retail, catálogo local).</summary>
    public static string? ResolveImage(string? code, string? name)
        => ResolveImage(CategoryVisualKind.Commerce, code, name);

    /// <summary>Elige la imagen según el contexto: comercio, servicios de empresas o profesionales.</summary>
    public static string? ResolveImage(CategoryVisualKind kind, string? code, string? name)
    {
        return kind switch
        {
            CategoryVisualKind.Commerce => ResolveCommerceImage(code, name),
            CategoryVisualKind.ServiceBusiness => ResolveServiceBusinessImage(code, name),
            CategoryVisualKind.Professional => ResolveProfessionalImage(code, name),
            _ => ResolveCommerceImage(code, name)
        };
    }

    private static string? ResolveCommerceImage(string? code, string? name)
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
        var fromKeywords = ResolveCommerceImageKeywords(normalizedName);
        if (!string.IsNullOrWhiteSpace(fromKeywords))
        {
            return fromKeywords;
        }

        return normalizedName switch
        {
            "alimentos y bebidas" => ImgAlimentos,
            "alimentos" => ImgAlimentos,
            "bebes y ninos" => ImgRegalos,
            "hogar y limpieza" => ImgHogar,
            "salud y cuidado personal" => ImgSalud,
            "moda y accesorios" => ImgModa,
            "tecnologia y accesorios" => ImgTecnologia,
            "regalos y celebraciones" => ImgRegalos,
            "deportes y aire libre" => ImgDeporte,
            "libreria y oficina" => ImgLibreria,
            "artesania y emprendimientos" => ImgArtesania,
            "inmuebles" => ImgHogar,
            _ => ResolveFallbackImage(code, name)
        };
    }

    private static string? ResolveServiceBusinessImage(string? slug, string? title)
    {
        var combined = MergeNormalized(slug, title);
        var fromKeywords = ResolveServiceBusinessKeywords(combined);
        if (!string.IsNullOrWhiteSpace(fromKeywords))
        {
            return fromKeywords;
        }

        if (TryResolveImageFromMap(slug, out var fromCode))
        {
            return fromCode;
        }

        if (TryResolveImageFromMap(title, out var fromName))
        {
            return fromName;
        }

        return ResolveFallbackImage(slug, title);
    }

    private static string? ResolveProfessionalImage(string? slug, string? title)
    {
        var combined = MergeNormalized(slug, title);
        var fromKeywords = ResolveProfessionalImageKeywords(combined);
        if (!string.IsNullOrWhiteSpace(fromKeywords))
        {
            return fromKeywords;
        }

        if (TryResolveImageFromMap(slug, out var fromCode))
        {
            return fromCode;
        }

        if (TryResolveImageFromMap(title, out var fromName))
        {
            return fromName;
        }

        return ResolveFallbackImage(slug, title);
    }

    private static string MergeNormalized(string? a, string? b)
    {
        var x = Normalize(a);
        var y = Normalize(b);
        if (string.IsNullOrWhiteSpace(x))
        {
            return y;
        }

        if (string.IsNullOrWhiteSpace(y))
        {
            return x;
        }

        return $"{x} {y}";
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

    /// <summary>Palabras clave para categorías de comercio (producto, tienda, retail local).</summary>
    private static string? ResolveCommerceImageKeywords(string normalized)
    {
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return normalized switch
        {
            var s when s.Contains("alimento", StringComparison.Ordinal) || s.Contains("bebida", StringComparison.Ordinal) || s.Contains("comida", StringComparison.Ordinal) || s.Contains("supermerc", StringComparison.Ordinal) || s.Contains("minimarket", StringComparison.Ordinal)
                => ImgAlimentos,
            var s when s.Contains("comercio", StringComparison.Ordinal) || s.Contains("tienda", StringComparison.Ordinal) || s.Contains("retail", StringComparison.Ordinal) || s.Contains("boutique", StringComparison.Ordinal) || s.Contains("gondola", StringComparison.Ordinal)
                => ImgModa,
            var s when s.Contains("hogar", StringComparison.Ordinal) || s.Contains("limpieza", StringComparison.Ordinal) || s.Contains("ferreter", StringComparison.Ordinal)
                => ImgHogar,
            var s when s.Contains("mejoramiento", StringComparison.Ordinal) || s.Contains("jardiner", StringComparison.Ordinal) || s.Contains("jardin", StringComparison.Ordinal) || s.Contains("herramient", StringComparison.Ordinal) || s.Contains("bricol", StringComparison.Ordinal)
                => ImgHogar,
            var s when s.Contains("salud", StringComparison.Ordinal) || s.Contains("cuidado", StringComparison.Ordinal) || s.Contains("wellness", StringComparison.Ordinal) || s.Contains("perfumer", StringComparison.Ordinal)
                => ImgSalud,
            var s when s.Contains("moda", StringComparison.Ordinal) || s.Contains("fashion", StringComparison.Ordinal) || s.Contains("vestir", StringComparison.Ordinal)
                => ImgModa,
            var s when s.Contains("tecnologia", StringComparison.Ordinal) || s.Contains("tech", StringComparison.Ordinal) || s.Contains("computac", StringComparison.Ordinal)
                => ImgTecnologia,
            var s when s.Contains("regalo", StringComparison.Ordinal) || s.Contains("celebr", StringComparison.Ordinal) || s.Contains("bebe", StringComparison.Ordinal) || s.Contains("nino", StringComparison.Ordinal)
                => ImgRegalos,
            var s when s.Contains("deporte", StringComparison.Ordinal) || s.Contains("sport", StringComparison.Ordinal) || s.Contains("aire libre", StringComparison.Ordinal)
                => ImgDeporte,
            var s when s.Contains("libreria", StringComparison.Ordinal) || s.Contains("oficina", StringComparison.Ordinal) || s.Contains("papeleria", StringComparison.Ordinal) || s.Contains("librer", StringComparison.Ordinal)
                => ImgLibreria,
            var s when s.Contains("artesania", StringComparison.Ordinal) || s.Contains("emprend", StringComparison.Ordinal) || s.Contains("craft", StringComparison.Ordinal)
                => ImgArtesania,
            var s when s.Contains("inmueble", StringComparison.Ordinal) || s.Contains("propiedad", StringComparison.Ordinal) || s.Contains("terreno", StringComparison.Ordinal) || s.Contains("parcela", StringComparison.Ordinal) || s.Contains("departamento", StringComparison.Ordinal)
                => ImgHogar,
            _ => null
        };
    }

    /// <summary>Imágenes alineadas a servicios ofrecidos por negocios (taller, clínica, local de servicio).</summary>
    private static string? ResolveServiceBusinessKeywords(string normalized)
    {
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return normalized switch
        {
            var s when s.Contains("construc", StringComparison.Ordinal) || s.Contains("albanil", StringComparison.Ordinal) || s.Contains("reparac", StringComparison.Ordinal) && s.Contains("casa", StringComparison.Ordinal)
                => ImgHogar,
            var s when s.Contains("peluqu", StringComparison.Ordinal) || s.Contains("estetic", StringComparison.Ordinal) || s.Contains("barber", StringComparison.Ordinal) || s.Contains("spa", StringComparison.Ordinal)
                => ImgModa,
            var s when s.Contains("farmac", StringComparison.Ordinal) || s.Contains("droguer", StringComparison.Ordinal) || s.Contains("clinic", StringComparison.Ordinal) || s.Contains("salud", StringComparison.Ordinal) && s.Contains("servic", StringComparison.Ordinal)
                => ImgSalud,
            var s when s.Contains("alimento", StringComparison.Ordinal) && (s.Contains("bar", StringComparison.Ordinal) || s.Contains("restaur", StringComparison.Ordinal) || s.Contains("cocina", StringComparison.Ordinal)) || s.Contains("gastronom", StringComparison.Ordinal)
                => ImgAlimentos,
            var s when s.Contains("agric", StringComparison.Ordinal) || s.Contains("agroneg", StringComparison.Ordinal) || s.Contains("ganader", StringComparison.Ordinal) || s.Contains("campo", StringComparison.Ordinal)
                => ImgAlimentos,
            var s when s.Contains("automot", StringComparison.Ordinal) || s.Contains("mecan", StringComparison.Ordinal) || s.Contains("taller", StringComparison.Ordinal) || s.Contains("neumatic", StringComparison.Ordinal)
                => ImgTecnologia,
            var s when s.Contains("educac", StringComparison.Ordinal) || s.Contains("curso", StringComparison.Ordinal) || s.Contains("coleg", StringComparison.Ordinal) || s.Contains("capacit", StringComparison.Ordinal)
                => ImgLibreria,
            var s when s.Contains("tecnologia", StringComparison.Ordinal) || s.Contains("computac", StringComparison.Ordinal) || s.Contains("soporte it", StringComparison.Ordinal)
                => ImgTecnologia,
            var s when s.Contains("limpieza", StringComparison.Ordinal) || s.Contains("fumig", StringComparison.Ordinal) || s.Contains("mantenc", StringComparison.Ordinal)
                => ImgHogar,
            var s when s.Contains("inmobil", StringComparison.Ordinal) || s.Contains("corredor", StringComparison.Ordinal) || s.Contains("arriendo", StringComparison.Ordinal)
                => ImgHogar,
            _ => null
        };
    }

    /// <summary>Imágenes alineadas a servicios profesionales (estudio, consulta, oficio técnico).</summary>
    private static string? ResolveProfessionalImageKeywords(string normalized)
    {
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return normalized switch
        {
            var s when s.Contains("topograf", StringComparison.Ordinal) || s.Contains("agrimen", StringComparison.Ordinal) || s.Contains("catastr", StringComparison.Ordinal) || s.Contains("geom", StringComparison.Ordinal)
                => ImgHogar,
            var s when s.Contains("electr", StringComparison.Ordinal) && (s.Contains("instalac", StringComparison.Ordinal) || s.Contains("ic", StringComparison.Ordinal) || s.Contains("certif", StringComparison.Ordinal))
                => ImgTecnologia,
            var s when s.Contains("arquitect", StringComparison.Ordinal) || (s.Contains("disen", StringComparison.Ordinal) && s.Contains("proyecto", StringComparison.Ordinal))
                => ImgArtesania,
            var s when s.Contains("abogad", StringComparison.Ordinal) || s.Contains("derech", StringComparison.Ordinal) || s.Contains("jurid", StringComparison.Ordinal) || s.Contains("legal", StringComparison.Ordinal) || s.Contains("notar", StringComparison.Ordinal)
                => ImgLibreria,
            var s when s.Contains("psicolog", StringComparison.Ordinal) || s.Contains("psiqui", StringComparison.Ordinal) || s.Contains("bienest", StringComparison.Ordinal) && s.Contains("terap", StringComparison.Ordinal)
                => ImgSalud,
            var s when s.Contains("veterin", StringComparison.Ordinal) || s.Contains("mascot", StringComparison.Ordinal)
                => ImgSalud,
            var s when s.Contains("ingeni", StringComparison.Ordinal) || s.Contains("civil", StringComparison.Ordinal) && s.Contains("estructur", StringComparison.Ordinal)
                => ImgTecnologia,
            var s when s.Contains("contad", StringComparison.Ordinal) || s.Contains("contabil", StringComparison.Ordinal) || s.Contains("tribut", StringComparison.Ordinal) || s.Contains("auditor", StringComparison.Ordinal)
                => ImgLibreria,
            var s when s.Contains("enferm", StringComparison.Ordinal) || s.Contains("kinesi", StringComparison.Ordinal) || s.Contains("fisio", StringComparison.Ordinal) || s.Contains("matrona", StringComparison.Ordinal)
                => ImgSalud,
            var s when s.Contains("entrenad", StringComparison.Ordinal) || s.Contains("kinesiolog", StringComparison.Ordinal) && s.Contains("deporte", StringComparison.Ordinal)
                => ImgDeporte,
            var s when s.Contains("medioambient", StringComparison.Ordinal) || s.Contains("sustentab", StringComparison.Ordinal) || s.Contains("ecolog", StringComparison.Ordinal) || s.Contains("gestion ambiental", StringComparison.Ordinal)
                => ImgDeporte,
            var s when s.Contains("construc", StringComparison.Ordinal) && (s.Contains("civil", StringComparison.Ordinal) || s.Contains("cálculo", StringComparison.Ordinal) || s.Contains("inspecc", StringComparison.Ordinal) || s.Contains("obra", StringComparison.Ordinal))
                => ImgHogar,
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
