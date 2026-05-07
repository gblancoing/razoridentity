using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RazorIdentity.Models.TallerCostos
{
    public class TallerAnalisis
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid ProyectoId { get; set; }
        public Guid? RevisionRiesgosId { get; set; }

        [MaxLength(300)]
        public string NombreProyecto { get; set; } = "";

        [MaxLength(50)]
        public string CodigoProyecto { get; set; } = "";

        public DateOnly FechaEjercicio { get; set; }

        public int Iteraciones { get; set; } = 10000;
        public int? SemillaAleatoria { get; set; }

        [Column(TypeName = "text")]
        public string ResultadosJson { get; set; } = "{}";

        public DateTime FechaEjecucion { get; set; } = DateTime.UtcNow;

        [MaxLength(450)]
        public string UsuarioId { get; set; } = "";

        public TallerProyecto Proyecto { get; set; } = null!;
    }
}
