using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RazorIdentity.Models.TallerCostos
{
    public class TallerRiesgo
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid RevisionId { get; set; }

        public int Orden { get; set; } = 1;

        [MaxLength(100)]
        public string Origen { get; set; } = "Proyecto";

        [MaxLength(50)]
        public string CodigoRiesgo { get; set; } = "";

        [MaxLength(200)]
        public string Titulo { get; set; } = "";

        [MaxLength(1000)]
        public string Descripcion { get; set; } = "";

        [Column(TypeName = "numeric(18,2)")]
        public decimal ImpactoProableUsd { get; set; }

        /// <summary>Impacto mínimo para distribución triangular en MC. Si null, usa 80% del probable.</summary>
        [Column(TypeName = "numeric(18,2)")]
        public decimal? ImpactoMinUsd { get; set; }

        /// <summary>Impacto máximo para distribución triangular en MC. Si null, usa 120% del probable.</summary>
        [Column(TypeName = "numeric(18,2)")]
        public decimal? ImpactoMaxUsd { get; set; }

        /// <summary>Probabilidad de ocurrencia en porcentaje (0-100). Null = 100 (certero).</summary>
        [Column(TypeName = "numeric(5,2)")]
        public decimal? Probabilidad { get; set; }

        /// <summary>true = Oportunidad (reduce EAT), false = Riesgo (aumenta EAT)</summary>
        public bool EsOportunidad { get; set; } = false;

        [MaxLength(500)]
        public string NotasCambio { get; set; } = "";

        /// <summary>Referencia a ítem del Bloque D marcado como ViaRiesgo</summary>
        public Guid? ItemViaRiesgoId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public TallerRevisionRiesgos Revision { get; set; } = null!;
    }
}
