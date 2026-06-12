namespace ComunaClick.Api.Persistence.Entities;

/// <summary>
/// Repartidor propio de un negocio. No tiene cuenta de usuario: opera con un
/// link seguro por pedido que el vendedor le comparte (ej. por WhatsApp).
/// </summary>
public sealed class Courier
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid PartnerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    /// <summary>Empresa externa a la que pertenece el repartidor (opcional).</summary>
    public string? Company { get; set; }

    /// <summary>Tipo de transportista: courier | taxi | ... (extensible a futuro).</summary>
    public string Kind { get; set; } = "courier";
    public bool IsAvailable { get; set; } = true;
    public double? CurrentLat { get; set; }
    public double? CurrentLng { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Partner Partner { get; set; } = null!;
}
