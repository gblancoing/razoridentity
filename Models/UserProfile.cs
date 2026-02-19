using System.ComponentModel.DataAnnotations;

namespace RazorIdentity.Models
{
    /// <summary>
    /// Datos personales extendidos del usuario (dirección, redes, etc.).
    /// Se relaciona 1:1 con AspNetUsers por UserId.
    /// </summary>
    public class UserProfile
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(450)]
        public string UserId { get; set; } = string.Empty;

        [MaxLength(200)]
        [Display(Name = "Nombre completo")]
        public string? FullName { get; set; }

        [MaxLength(500)]
        [Display(Name = "Dirección")]
        public string? Address { get; set; }

        [MaxLength(150)]
        [Display(Name = "Ciudad")]
        public string? City { get; set; }

        [MaxLength(150)]
        [Display(Name = "Región o estado")]
        public string? RegionOrState { get; set; }

        [MaxLength(100)]
        [Display(Name = "Código postal")]
        public string? PostalCode { get; set; }

        [MaxLength(150)]
        [Display(Name = "País")]
        public string? Country { get; set; }

        [MaxLength(500)]
        [Url(ErrorMessage = "Debe ser una URL válida.")]
        [Display(Name = "Perfil de LinkedIn")]
        public string? LinkedInUrl { get; set; }

        [MaxLength(500)]
        [Url(ErrorMessage = "Debe ser una URL válida.")]
        [Display(Name = "Sitio web personal")]
        public string? WebsiteUrl { get; set; }

        [MaxLength(500)]
        [Display(Name = "Otros enlaces o redes")]
        public string? OtherLinks { get; set; }

        [MaxLength(1000)]
        [Display(Name = "Notas o biografía breve")]
        public string? BioOrNotes { get; set; }
    }
}
