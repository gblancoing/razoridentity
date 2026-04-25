namespace ComunaClick.SharedUI.Data;

/// <summary>Entradas de exploración: diseño tipo categorías; el destino es categoría publicada o búsqueda en inicio.</summary>
/// <param name="CommerceImageKey">Clave del catálogo de comercio (ImageMap) si no hay <paramref name="ImageUrl"/>.</param>
/// <param name="ImageUrl">
/// Opcional. URL de imagen (https) o ruta bajo <c>_content/ComunaClick.SharedUI/...</c> para un archivo tuyo.
/// Si la defines, reemplaza el comercio genérico y evita que varias filas compartan la misma foto.
/// </param>
public sealed record DiscoveryHubItem(
    string Title,
    string Slug,
    string? CategoryCode = null,
    string? SearchQuery = null,
    string? CommerceImageKey = null,
    string? ImageUrl = null);

public static class DiscoveryHubCatalog
{
    // Fotos de referencia (Unsplash) por rubro. Puedes sustituir cada ImageUrl por:
    // "_content/ComunaClick.SharedUI/hub-images/tu-archivo.png" tras añadir el PNG al proyecto.
    public static IReadOnlyList<DiscoveryHubItem> ServiceSectors { get; } =
    [
        // Primeras filas: salud, agro, automotriz y clínicas (muy vistos; antes quedaban bajo scroll en pantallas bajas)
        // Mismas plantillas de URL Unsplash que el resto de filas (evita IDs sueltos que devuelven 404).
        new("Farmacias y salud", "farmacias", null, "farmacia", "health-personal-care",
            "https://images.unsplash.com/photo-1576091160550-2173dba999ef?auto=format&fit=crop&w=800&q=75"),
        new("Agronegocios y campo", "agronegocios", null, "agricultura agronegocio", "food-beverages",
            "https://images.unsplash.com/photo-1414235077428-338989a2e8c0?auto=format&fit=crop&w=800&q=75"),
        new("Automotriz y mecánica", "automotriz", null, "taller mecánica automotriz", "tech-accessories",
            "https://images.unsplash.com/photo-1517694712202-14dd9538aa97?auto=format&fit=crop&w=800&q=75"),
        new("Clínicas y salud", "clinicas-salud", "health-care", "clínica salud", null,
            "https://images.unsplash.com/photo-1506126613408-eca07ce68773?auto=format&fit=crop&w=800&q=75"),
        new("Construcción y obras", "construccion-empresas", null, "construcción", "home-cleaning",
            "https://images.unsplash.com/photo-1541888946425-d81bb19240f5?auto=format&fit=crop&w=800&q=75"),
        new("Peluquerías y estética", "peluquerias", null, "peluquería belleza", "fashion-accessories",
            "https://images.unsplash.com/photo-1560066984-138dadb4c035?auto=format&fit=crop&w=800&q=75"),
        new("Alimentos y bar", "alimentos-bar", "food-beverages", "alimentos restaurant", null,
            "https://images.unsplash.com/photo-1414235077428-338989a2e8c0?auto=format&fit=crop&w=800&q=75"),
        new("Educación y cursos", "educacion", null, "educación curso", "stationery-office",
            "https://images.unsplash.com/photo-1503676260728-1c00da094a0b?auto=format&fit=crop&w=800&q=75"),
        new("Tecnología y computación", "tecnologia", "tech-accessories", "tecnología reparación", null,
            "https://images.unsplash.com/photo-1517694712202-14dd9538aa97?auto=format&fit=crop&w=800&q=75"),
        new("Hogar y reparación", "hogar-reparacion", "home-cleaning", "hogar mantención", null,
            "https://images.unsplash.com/photo-1621905251918-48416bd8575a?auto=format&fit=crop&w=800&q=75"),
        new("Limpieza y mantención", "limpieza", null, "limpieza mantención", "home-cleaning",
            "https://images.unsplash.com/photo-1581578731548-c64695cc6952?auto=format&fit=crop&w=800&q=75"),
        new("Inmobiliaria e inmuebles", "inmobiliaria", "inmuebles", "inmobiliaria", null,
            "https://images.unsplash.com/photo-1560518883-ce09059eeffa?auto=format&fit=crop&w=800&q=75")
    ];

    public static IReadOnlyList<DiscoveryHubItem> ProfessionalSectors { get; } =
    [
        new("Arquitectura", "arquitectura", null, "arquitecto proyectos", "crafts-entrepreneurs",
            "https://images.unsplash.com/photo-1560518883-ce09059eeffa?auto=format&fit=crop&w=800&q=75"),
        new("Topografía y agrimensura", "topografia", null, "topografía agrimensura", "inmuebles",
            "https://images.unsplash.com/photo-1503387762-592deb58ef4e?auto=format&fit=crop&w=800&q=75"),
        new("Electricidad", "electricidad", null, "electricidad instalación", "tech-accessories",
            "https://images.unsplash.com/photo-1621905252507-b35492cc74b4?auto=format&fit=crop&w=800&q=75"),
        new("Obras y construcción (IC)", "obras-construccion-ic", null, "construcción cálculo", "home-cleaning",
            "https://images.unsplash.com/photo-1504307651254-35680f356dfd?auto=format&fit=crop&w=800&q=75"),
        new("Derecho y asesoría legal", "derecho", null, "abogado asesoría legal", "stationery-office",
            "https://images.unsplash.com/photo-1589829545856-d10d557cf95f?auto=format&fit=crop&w=800&q=75"),
        new("Psicología y bienestar", "psicologia", null, "psicólogo psicología", "health-personal-care",
            "https://images.unsplash.com/photo-1506126613408-eca07ce68773?auto=format&fit=crop&w=800&q=75"),
        new("Veterinaria y mascotas", "veterinaria", null, "veterinario", "health-personal-care",
            "https://images.unsplash.com/photo-1628009368231-7bb7cfcb0def?auto=format&fit=crop&w=800&q=75"),
        new("Ingeniería (civil, industrial, etc.)", "ingenieria", null, "ingeniería", "tech-accessories",
            "https://images.unsplash.com/photo-1504917595217-d4dc5ebe6122?auto=format&fit=crop&w=800&q=75"),
        new("Contabilidad y finanzas", "contabilidad", null, "contador contabilidad", "stationery-office",
            "https://images.unsplash.com/photo-1554224155-8d04cb21cd6c?auto=format&fit=crop&w=800&q=75"),
        new("Enfermería y salud", "enfermeria", null, "enfermería", "health-care",
            "https://images.unsplash.com/photo-1576091160550-2173dba999ef?auto=format&fit=crop&w=800&q=75"),
        new("Entrenamiento y deporte", "entrenamiento", null, "entrenador kinesiología", "sports-outdoors",
            "https://images.unsplash.com/photo-1517838277536-f5f99be501cd?auto=format&fit=crop&w=800&q=75"),
        new("Medioambiente y sustentabilidad", "medioambiente", null, "medioambiente", "sports-outdoors",
            "https://images.unsplash.com/photo-1441974231531-c6227db76b6e?auto=format&fit=crop&w=800&q=75")
    ];

    public static DiscoveryHubItem? FindProfessionalBySlug(string? slug) =>
        string.IsNullOrWhiteSpace(slug)
            ? null
            : ProfessionalSectors.FirstOrDefault(s =>
                string.Equals(s.Slug, slug.Trim(), StringComparison.OrdinalIgnoreCase));
}
