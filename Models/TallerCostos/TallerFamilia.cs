using System.ComponentModel.DataAnnotations;

namespace RazorIdentity.Models.TallerCostos
{
    public class TallerFamilia
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid ProyectoId { get; set; }

        [Required, MaxLength(150)]
        public string Nombre { get; set; } = "";

        [MaxLength(500)]
        public string? Descripcion { get; set; }

        public int Orden { get; set; } = 1;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public TallerProyecto Proyecto { get; set; } = null!;
        public ICollection<TallerContrato> Contratos { get; set; } = new List<TallerContrato>();
    }
}
