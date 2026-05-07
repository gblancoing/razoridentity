using System.ComponentModel.DataAnnotations;

namespace RazorIdentity.Models.TallerCostos
{
    /// <summary>Empresa contratista asociada a un proyecto del taller; un mismo proyecto puede tener varias y cada una varios paquetes (contratos).</summary>
    public class TallerEmpresa
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid ProyectoId { get; set; }

        /// <summary>RUT chileno u otro identificador fiscal (obligatorio en el taller).</summary>
        [Required, MaxLength(20)]
        public string Rut { get; set; } = "";

        [Required, MaxLength(300)]
        public string Nombre { get; set; } = "";

        [MaxLength(200)]
        public string? Contacto { get; set; }

        [MaxLength(200)]
        public string? Email { get; set; }

        [MaxLength(50)]
        public string? Telefono { get; set; }

        [MaxLength(500)]
        public string? Notas { get; set; }

        public int Orden { get; set; } = 1;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public TallerProyecto Proyecto { get; set; } = null!;
        public ICollection<TallerContrato> Contratos { get; set; } = new List<TallerContrato>();
    }
}
