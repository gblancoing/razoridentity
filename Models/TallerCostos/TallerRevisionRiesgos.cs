using System.ComponentModel.DataAnnotations;

namespace RazorIdentity.Models.TallerCostos
{
    public class TallerRevisionRiesgos
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid ProyectoId { get; set; }

        public int NumeroRevision { get; set; } = 1;

        [MaxLength(200)]
        public string Descripcion { get; set; } = "";

        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

        [MaxLength(450)]
        public string UsuarioId { get; set; } = "";

        public TallerProyecto Proyecto { get; set; } = null!;
        public ICollection<TallerRiesgo> Riesgos { get; set; } = new List<TallerRiesgo>();
    }
}
