using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RazorIdentity.Models.TallerCostos
{
    public class TallerProyecto
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required, MaxLength(450)]
        public string UserId { get; set; } = "";

        [Required, MaxLength(300)]
        public string Nombre { get; set; } = "";

        [Required, MaxLength(50)]
        public string Codigo { get; set; } = "";

        [Required]
        public DateOnly FechaEjercicio { get; set; }

        [MaxLength(300)]
        public string Organizacion { get; set; } = "";

        [MaxLength(50)]
        public string Estado { get; set; } = "Borrador";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<TallerFamilia> Familias { get; set; } = new List<TallerFamilia>();
        public ICollection<TallerEmpresa> Empresas { get; set; } = new List<TallerEmpresa>();
        public ICollection<TallerContrato> Contratos { get; set; } = new List<TallerContrato>();
        public ICollection<TallerRevisionRiesgos> Revisiones { get; set; } = new List<TallerRevisionRiesgos>();
        public ICollection<TallerAnalisis> Analisis { get; set; } = new List<TallerAnalisis>();
    }
}
