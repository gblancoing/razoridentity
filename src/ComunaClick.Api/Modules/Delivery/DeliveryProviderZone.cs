using ComunaClick.Api.Persistence.Entities;

namespace ComunaClick.Api.Modules.Delivery;

/// <summary>
/// Filtro de cobertura de transportistas: activo, del tenant del negocio (o
/// global) y cuya zona aplica al negocio (comuna exacta, su región, o sin
/// zona = cobertura global). Mismo criterio que valida OrderCheckoutService.
/// </summary>
public static class DeliveryProviderZone
{
    public static IQueryable<DeliveryProvider> WhereServesPartner(this IQueryable<DeliveryProvider> query, Partner partner)
        => query.Where(x =>
            x.IsActive &&
            (!x.TenantId.HasValue || x.TenantId.Value == partner.TenantId) &&
            (
                (x.ComunaId.HasValue && partner.ComunaId.HasValue && x.ComunaId.Value == partner.ComunaId.Value) ||
                (!x.ComunaId.HasValue && x.RegionId.HasValue && partner.RegionId.HasValue && x.RegionId.Value == partner.RegionId.Value) ||
                (!x.ComunaId.HasValue && !x.RegionId.HasValue)
            ));

    /// <summary>Orden de especificidad: comuna > región > global.</summary>
    public static IOrderedQueryable<DeliveryProvider> OrderByZoneSpecificity(this IQueryable<DeliveryProvider> query)
        => query
            .OrderByDescending(x => x.ComunaId.HasValue)
            .ThenByDescending(x => x.RegionId.HasValue)
            .ThenBy(x => x.Name);
}
