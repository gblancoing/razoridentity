namespace RazorIdentity.Models;

public class NivelProbabilidad
{
    public int    Nivel             { get; set; }
    public string Cualitativo       { get; set; } = "";
    public double RangoMinPct       { get; set; }
    public double RangoMaxPct       { get; set; }
    public double PctRepresentativo { get; set; }
    public string ColorHex          { get; set; } = "#9CA3AF";
}

/// <summary>
/// Tabla oficial CODELCO de probabilidad de ocurrencia — columna Proyectos.
/// Fuente: Vicepresidencia de Proyectos CODELCO.
/// </summary>
public static class CodelcoProbabilidadTable
{
    public static readonly IReadOnlyList<NivelProbabilidad> Niveles = new[]
    {
        new NivelProbabilidad { Nivel = 5, Cualitativo = "Casi Seguro",   RangoMinPct = 75,  RangoMaxPct = 100, PctRepresentativo = 90,   ColorHex = "#DC2626" },
        new NivelProbabilidad { Nivel = 4, Cualitativo = "Muy Probable",  RangoMinPct = 50,  RangoMaxPct = 75,  PctRepresentativo = 62.5, ColorHex = "#F97316" },
        new NivelProbabilidad { Nivel = 3, Cualitativo = "Probable",      RangoMinPct = 30,  RangoMaxPct = 50,  PctRepresentativo = 40,   ColorHex = "#FACC15" },
        new NivelProbabilidad { Nivel = 2, Cualitativo = "Poco Probable", RangoMinPct = 10,  RangoMaxPct = 30,  PctRepresentativo = 20,   ColorHex = "#60A5FA" },
        new NivelProbabilidad { Nivel = 1, Cualitativo = "Remoto",        RangoMinPct = 0,   RangoMaxPct = 10,  PctRepresentativo = 5,    ColorHex = "#9CA3AF" },
    };

    /// <summary>
    /// Retorna el nivel CODELCO para un porcentaje dado (0–100).
    /// Ej: 90 → Nivel 5 "Casi Seguro" | 5 → Nivel 1 "Remoto"
    /// </summary>
    public static NivelProbabilidad ObtenerNivel(double probabilidadPct) =>
        Niveles.FirstOrDefault(n => probabilidadPct > n.RangoMinPct && probabilidadPct <= n.RangoMaxPct)
        ?? Niveles[^1]; // 0% cae en Remoto

    /// <summary>Retorna el nivel CODELCO por número (1–5).</summary>
    public static NivelProbabilidad ObtenerNivelPorNumero(int nivel) =>
        Niveles.FirstOrDefault(n => n.Nivel == nivel) ?? Niveles[^1];
}
