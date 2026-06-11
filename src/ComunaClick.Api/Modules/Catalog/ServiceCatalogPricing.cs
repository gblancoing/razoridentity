using ComunaClick.Api.Persistence.Entities;

namespace ComunaClick.Api.Modules.Catalog;

internal static class ServiceCatalogPricing
{
    public static void Apply(Service service, decimal price)
    {
        service.Price = price;
        var bookable = price > 0;
        service.IsBookable = bookable;
        service.RequiresOnlinePayment = bookable;
    }
}
