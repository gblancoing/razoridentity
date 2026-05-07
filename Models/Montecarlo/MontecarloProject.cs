using System.ComponentModel.DataAnnotations;

namespace RazorIdentity.Models.Montecarlo;

public class MontecarloProject
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(450)]
    public string UserId { get; set; } = "";

    [Required, MaxLength(300)]
    public string ProjectName { get; set; } = "";

    [MaxLength(50)]
    public string Status { get; set; } = "InProgress";

    // JSON serializado del diccionario Dictionary<string, ProjectTabData>
    public string TabsJson { get; set; } = "{}";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
