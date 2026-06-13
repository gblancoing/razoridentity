using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace ComunaClick.Api.Persistence.Entities;

public sealed class OrderItem
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }

    /// <summary>Nombre del producto (de Products); solo para respuestas del panel partner.</summary>
    [NotMapped]
    public string? ProductName { get; set; }

    // JsonIgnore: evita el ciclo Order → Items → Order al serializar respuestas de la API.
    [JsonIgnore]
    public Order Order { get; set; } = null!;
}
