namespace RazorIdentity.Services;

/// <summary>Catálogos y rutas alineados a la especificación Vectores Financieros (PHP → REST PYC).</summary>
public static class PycFinancierosCatalog
{
    public static readonly IReadOnlyList<(string Id, string Etiqueta, string Tabla)> ModosVector =
    [
        ("real_parcial", "Real Parcial", "real_parcial"),
        ("real_acumulado", "Real Acumulado", "real_acumulado"),
        ("v0_parcial", "V0 Parcial", "v0_parcial"),
        ("v0_acumulada", "V0 Acumulada", "v0_acumulada"),
        ("npc_parcial", "NPC Parcial", "npc_parcial"),
        ("npc_acumulado", "NPC Acumulado", "npc_acumulado"),
        ("api_parcial", "API Parcial", "api_parcial"),
        ("api_acumulada", "API Acumulada", "api_acumulada"),
    ];

    /// <summary>Modos vector mostrados en la barra PMO (solo parciales; acumulados ocultos en UI).</summary>
    public static readonly IReadOnlyList<(string Id, string Etiqueta, string Tabla)> ModosVectorBarraPmo =
    [
        ("real_parcial", "Real Parcial", "real_parcial"),
        ("v0_parcial", "V0 Parcial", "v0_parcial"),
        ("npc_parcial", "NPC Parcial", "npc_parcial"),
        ("api_parcial", "API Parcial", "api_parcial"),
    ];

    public static readonly IReadOnlyList<(string Id, string Etiqueta, string Tabla)> ModosAnalisis =
    [
        ("reporte1", "Curva S - Parcial / Acum", "reporte1"),
        ("reporte9", "Flujo Financiero SAP", "financiero_sap"),
        ("av_fisico_c9", "Project 9C", "vc_project_9c"),
    ];

    /// <summary>Ocho categorías canónicas KPI (orden fijo).</summary>
    public static readonly IReadOnlyList<string> CategoriasKpi =
    [
        "CONSTRUCCION",
        "INDIRECTOS DE CONTRATISTAS",
        "EQUIPOS Y MATERIALES",
        "INGENIERIA",
        "SERVICIOS DE APOYO A LA CONSTRUCCION",
        "ADM. DEL PROYECTO",
        "COSTOS ESPECIALES",
        "CONTINGENCIA",
    ];

    /// <summary>Mapa código SAP → categoría larga.</summary>
    public static readonly IReadOnlyDictionary<string, string> CodigoSapACategoria = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["MO"] = "CONSTRUCCION",
        ["IC"] = "INDIRECTOS DE CONTRATISTAS",
        ["EM"] = "EQUIPOS Y MATERIALES",
        ["IE"] = "INGENIERIA",
        ["SC"] = "SERVICIOS DE APOYO A LA CONSTRUCCION",
        ["AD"] = "ADM. DEL PROYECTO",
        ["CL"] = "COSTOS ESPECIALES",
        ["CT"] = "CONTINGENCIA",
    };

    /// <summary>Nombre de archivo PHP bajo <c>api/importaciones/</c> (mismo contrato POST <c>proyecto_id</c> + <c>rows</c>).</summary>
    public static string ImportacionSegmentForVector(string modoId)
    {
        return modoId switch
        {
            "real_parcial" => "importar_real_parcial.php",
            "real_acumulado" => "importar_real_acumulado.php",
            "v0_parcial" => "importar_v0_parcial.php",
            "v0_acumulada" => "importar_v0_acumulado.php",
            "npc_parcial" => "importar_npc_parcial.php",
            "npc_acumulado" => "importar_npc_acumulado.php",
            "api_parcial" => "importar_api_parcial.php",
            "api_acumulada" => "importar_api_acumulado.php",
            _ => throw new ArgumentOutOfRangeException(nameof(modoId))
        };
    }

    public const string ImportSapSegment = "importar_financiero_sap.php";
    public const string Import9cSegment = "importar_av_project_c9.php";

    /// <summary>Rutas sin <c>.php</c> para una API .NET que replique el contrato.</summary>
    public static string ImportacionSegmentForVectorAspNet(string modoId)
    {
        return modoId switch
        {
            "real_parcial" => "importar-real-parcial",
            "real_acumulado" => "importar-real-acumulado",
            "v0_parcial" => "importar-v0-parcial",
            "v0_acumulada" => "importar-v0-acumulado",
            "npc_parcial" => "importar-npc-parcial",
            "npc_acumulado" => "importar-npc-acumulado",
            "api_parcial" => "importar-api-parcial",
            "api_acumulada" => "importar-api-acumulado",
            _ => throw new ArgumentOutOfRangeException(nameof(modoId))
        };
    }

    public const string ImportSapSegmentAspNet = "importar-financiero-sap";
    public const string Import9cSegmentAspNet = "importar-av-project-c9";
}
