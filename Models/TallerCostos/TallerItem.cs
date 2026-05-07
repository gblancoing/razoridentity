using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RazorIdentity.Models.TallerCostos
{
    /// <summary>
    /// Representa un ítem del "Por Comprometer" (Bloque B).
    /// También contiene los campos de Bloque C (Clase Estimación),
    /// Bloque D (Contingencia) y Bloque E (Factores de Rango).
    /// </summary>
    public class TallerItem
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid ContratoId { get; set; }
        public int Orden { get; set; } = 1;

        // ── Bloque B ─────────────────────────────────────────────────────────
        [MaxLength(50)]
        public string CodigoItem { get; set; } = "";

        [MaxLength(500)]
        public string Descripcion { get; set; } = "";

        [MaxLength(20)]
        public string Unidad { get; set; } = "USD";

        [MaxLength(50)]
        public string SubPartida { get; set; } = "";

        public bool EsCerteza { get; set; } = false;
        public bool EsPorComprometer { get; set; } = true;

        [Column(TypeName = "numeric(18,2)")]
        public decimal CostoUsd { get; set; }

        /// <summary>Override manual del costo CLP (NDC u otras correcciones). Null = usar cálculo automático.</summary>
        [Column(TypeName = "numeric(18,2)")]
        public decimal? CostoClpOverride { get; set; }

        // ── Bloque C — Clase de Estimación ───────────────────────────────────
        [MaxLength(20)]
        public string ClaseEstimacion { get; set; } = "Clase 3";

        [MaxLength(500)]
        public string Consideraciones { get; set; } = "";

        // ── Bloque D — V. de Costo (solo ítems incertidumbre) ───────────────
        [Column(TypeName = "numeric(18,4)")]
        public decimal? MinPct { get; set; }

        [Column(TypeName = "numeric(18,4)")]
        public decimal? MinKusd { get; set; }

        [Column(TypeName = "numeric(18,4)")]
        public decimal? ProbablePct { get; set; }

        [Column(TypeName = "numeric(18,4)")]
        public decimal? ProbableKusd { get; set; }

        [Column(TypeName = "numeric(18,4)")]
        public decimal? MaxPct { get; set; }

        [Column(TypeName = "numeric(18,4)")]
        public decimal? MaxKusd { get; set; }

        public bool ViaRiesgo { get; set; } = false;

        // ── Bloque E — Factores de Rango (solo incertidumbre no-ViaRiesgo) ───
        [MaxLength(500)]
        public string Oportunidades { get; set; } = "";

        [MaxLength(500)]
        public string Amenazas { get; set; } = "";

        public decimal? Clase { get; set; }
        public decimal? Peso { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public TallerContrato Contrato { get; set; } = null!;

        // ── Propiedades calculadas ────────────────────────────────────────────
        [NotMapped]
        public decimal CalculoClase => (Clase ?? 0) * (Peso ?? 0);
    }
}
