using ComunaClick.Api.Persistence.Entities;

namespace ComunaClick.Api.Modules.Catalog;

internal static class ProductInventoryRules
{
    public static bool IsInStock(int availableQuantity) => availableQuantity > 0;

    public static int GetOnHandQuantity(ProductInventory? inventory) => inventory?.Quantity ?? 0;
}
