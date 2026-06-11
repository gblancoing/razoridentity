namespace ComunaClick.Api.Configuration;

public sealed class InventoryOptions
{
    public const string SectionName = "Inventory";

    /// <summary>Unidades disponibles en o por debajo de este valor generan alerta de stock bajo.</summary>
    public int LowStockThreshold { get; set; } = 5;

    /// <summary>Estados de pedido que reservan stock (sin descontar inventario físico).</summary>
    public string[] ReservingOrderStatuses { get; set; } =
    [
        "payment_pending",
        "processing"
    ];
}
