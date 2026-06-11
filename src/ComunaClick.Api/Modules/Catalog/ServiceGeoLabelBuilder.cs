using ComunaClick.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ComunaClick.Api.Modules.Catalog;

internal static class ServiceGeoLabelBuilder
{
    public static async Task<string?> BuildAsync(CoreDbContext db, Guid? comunaId, CancellationToken cancellationToken)
    {
        if (comunaId is not Guid id || id == Guid.Empty)
        {
            return null;
        }

        var comuna = await db.Comunas.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.IsActive, cancellationToken);
        if (comuna is null)
        {
            return null;
        }

        var region = await db.Regions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == comuna.RegionId && x.IsActive, cancellationToken);
        var country = region is null
            ? null
            : await db.Countries.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == region.CountryId && x.IsActive, cancellationToken);

        var parts = new List<string> { comuna.Name };
        if (!string.IsNullOrWhiteSpace(region?.Name))
        {
            parts.Add(region.Name);
        }

        if (!string.IsNullOrWhiteSpace(country?.Name))
        {
            parts.Add(country.Name);
        }

        return string.Join(", ", parts);
    }
}
