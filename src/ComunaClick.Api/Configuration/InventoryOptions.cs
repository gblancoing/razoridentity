namespace ComunaClick.Api.Configuration;

public sealed class InventoryOptions
{
    public const string SectionName = "Inventory";

    /// <summary>Unidades disponibles en o por debajo de este valor generan alerta de stock bajo.</summary>
    public int LowStockThreshold { get; set; } = 5;

    /// <summary>
    /// Estados de pedido que reservan stock (sin descontar inventario físico).
    /// Vacío por defecto: el disponible es siempre el stock físico y solo baja
    /// con una venta pagada o un ajuste manual de inventario.
    /// </summary>
    public string[] ReservingOrderStatuses { get; set; } = [];

    /// <summary>
    /// Minutos que una orden payment_pending retiene su reserva de stock. Pasado
    /// el plazo, la reserva se ignora en tiempo real al calcular disponible y el
    /// job de expiración cancela la orden formalmente. 0 = sin expiración.
    /// Si el pago llega después de expirar, el pago confirmado gana (el webhook
    /// repone el estado paid y el descuento es idempotente).
    /// </summary>
    public int PendingOrderTtlMinutes { get; set; } = 5;
}
