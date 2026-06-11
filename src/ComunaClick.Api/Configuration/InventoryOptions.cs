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
}
