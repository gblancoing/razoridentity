using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RazorIdentity.Models.TallerCostos
{
    public class TallerContrato
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid ProyectoId { get; set; }

        [MaxLength(20)]
        public string Codigo { get; set; } = ""; // CC001, CC002...

        public int Orden { get; set; } = 1;

        [MaxLength(300)]
        public string NombrePaquete { get; set; } = "";

        [Column(TypeName = "numeric(18,2)")]
        public decimal CapexUsd { get; set; }

        [Column(TypeName = "numeric(18,2)")]
        public decimal CompometidoUsd { get; set; }

        [Column(TypeName = "numeric(18,2)")]
        public decimal PorComprometidoUsd { get; set; }

        [Column(TypeName = "numeric(18,2)")]
        public decimal EstimadoTerminoUsd { get; set; }

        [Column(TypeName = "numeric(18,4)")]
        public decimal TasaCambio { get; set; } = 900m;

        [Column(TypeName = "numeric(20,15)")]
        public decimal Factor { get; set; } = 1m;

        public DateOnly? FechaTasaCambio { get; set; }

        public Guid? FamiliaId { get; set; }   // nullable → contratos sin familia asignada

        /// <summary>Empresa contratista del paquete (mismo proyecto).</summary>
        public Guid? EmpresaId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public TallerProyecto Proyecto { get; set; } = null!;
        public TallerFamilia? Familia { get; set; }
        public TallerEmpresa? Empresa { get; set; }
        public ICollection<TallerItem> Items { get; set; } = new List<TallerItem>();
    }
}
