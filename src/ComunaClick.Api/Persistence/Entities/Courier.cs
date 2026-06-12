namespace ComunaClick.Api.Persistence.Entities;

/// <summary>
/// Repartidor propio de un negocio. La entrega activa opera con un link seguro
/// por pedido (compartido p. ej. por WhatsApp); además, si el negocio registró
/// su correo, el repartidor puede crear una cuenta y reclamar este perfil para
/// ver sus viajes/ganancias y vincular su Mercado Pago (portal /account/courier).
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

    /// <summary>Correo del repartidor (lo registra el negocio); habilita el reclamo del portal.</summary>
    public string? Email { get; set; }

    /// <summary>Usuario que reclamó este perfil (FK lógica a ACL users).</summary>
    public Guid? UserId { get; set; }

    /// <summary>Tipo de transportista: courier | taxi | ... (extensible a futuro).</summary>
    public string Kind { get; set; } = "courier";
    public bool IsAvailable { get; set; } = true;
    public double? CurrentLat { get; set; }
    public double? CurrentLng { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Partner Partner { get; set; } = null!;
}
