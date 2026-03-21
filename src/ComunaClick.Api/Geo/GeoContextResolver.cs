using ComunaClick.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ComunaClick.Api.Geo;

public static class GeoContextResolver
{
    public static async Task<(Guid? CountryId, Guid? RegionId, Guid? ComunaId)> ResolveFromTenantAsync(CoreDbContext db, Guid tenantId)
    {
        var tenant = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(x => x.Id == tenantId);
        if (tenant?.ComunaId is null)
        {
            return (null, null, null);
        }

        var comuna = await db.Comunas.AsNoTracking().FirstOrDefaultAsync(x => x.Id == tenant.ComunaId.Value);
        if (comuna is null)
        {
            return (null, null, tenant.ComunaId);
        }

        var region = await db.Regions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == comuna.RegionId);
        return (region?.CountryId, comuna.RegionId, tenant.ComunaId);
    }
}
