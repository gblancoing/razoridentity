using System.ComponentModel.DataAnnotations.Schema;

namespace ComunaClick.Api.Persistence.Entities;

public sealed class Order
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid PartnerId { get; set; }
    public Guid CustomerId { get; set; }
    public string? ExternalReference { get; set; }
    public string Status { get; set; } = "payment_pending";
    public decimal Subtotal { get; set; }
    public decimal DeliveryFee { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal GrossAmount { get; set; }
    public decimal PlatformFeeAmount { get; set; }
    public decimal NetAmount { get; set; }
    public string Currency { get; set; } = "CLP";
    public string? BuyerEmail { get; set; }
    public string? BuyerName { get; set; }
    /// <summary>Teléfono del comprador (de Customer); solo para respuestas del panel partner.</summary>
    [NotMapped]
    public string? BuyerPhone { get; set; }

    /// <summary>Comisión real de Mercado Pago (de PaymentFees); solo para respuestas del panel partner.</summary>
    [NotMapped]
    public decimal? MercadoPagoFeeAmount { get; set; }
    public Guid? DeliveryProviderId { get; set; }
    public string? DeliveryProviderName { get; set; }
    /// <summary>Dirección de destino del envío (texto que ingresó el comprador).</summary>
    public string? DeliveryAddress { get; set; }
    /// <summary>"pickup" o "delivery" (DeliveryTypes).</summary>
    public string? DeliveryType { get; set; }
    /// <summary>Estado del envío (DeliveryStatuses); null si la orden no es delivery.</summary>
    public string? DeliveryStatus { get; set; }
    /// <summary>Repartidor asignado al envío.</summary>
    public Guid? CourierId { get; set; }
    /// <summary>Clave aleatoria por asignación: viaja dentro del HMAC del token del
    /// repartidor; regenerarla o limpiarla revoca todos los tokens anteriores.</summary>
    public string? CourierTokenKey { get; set; }
    public double? OriginLat { get; set; }
    public double? OriginLng { get; set; }
    public string? OriginAddress { get; set; }
    public double? DestinationLat { get; set; }
    public double? DestinationLng { get; set; }
    // Marca única de descuento de stock: garantiza que el fulfillment ocurra una sola vez.
    public DateTimeOffset? InventoryFulfilledAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}
