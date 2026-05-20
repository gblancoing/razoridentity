using ComunaClick.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ComunaClick.Api.Modules.Crm;

internal static class CustomerAddressHelper
{
    public static string? NormalizeAddress(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var t = raw.Trim();
        return t.Length > 500 ? t[..500] : t;
    }

    /// <summary>Aligns country/region/comuna when the client sent inconsistent ids (e.g. after map reverse-geocode).</summary>
    public static async Task<(Guid? CountryId, Guid? RegionId, Guid? ComunaId)> NormalizeGeoAsync(
        CoreDbContext db,
        Guid? countryId,
        Guid? regionId,
        Guid? comunaId,
        CancellationToken cancellationToken)
    {
        if (comunaId is Guid cId && cId != Guid.Empty)
        {
            var comuna = await db.Comunas.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == cId && x.IsActive, cancellationToken);
            if (comuna is null)
            {
                return (countryId, regionId, null);
            }

            regionId = comuna.RegionId;
            var region = await db.Regions.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == comuna.RegionId && x.IsActive, cancellationToken);
            if (region is not null)
            {
                countryId = region.CountryId;
            }

            return (countryId, regionId, comunaId);
        }

        if (regionId is Guid rId && rId != Guid.Empty)
        {
            var region = await db.Regions.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == rId && x.IsActive, cancellationToken);
            if (region is null)
            {
                return (countryId, null, null);
            }

            if (countryId is Guid coId && coId != Guid.Empty && region.CountryId != coId)
            {
                countryId = region.CountryId;
            }
            else if (!countryId.HasValue || countryId.Value == Guid.Empty)
            {
                countryId = region.CountryId;
            }

            return (countryId, regionId, null);
        }

        return (countryId, regionId, comunaId);
    }

    public static async Task<bool> ValidateGeoAsync(
        CoreDbContext db,
        Guid? countryId,
        Guid? regionId,
        Guid? comunaId,
        CancellationToken cancellationToken)
    {
        if (comunaId is Guid cId && cId != Guid.Empty)
        {
            var comuna = await db.Comunas.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == cId && x.IsActive, cancellationToken);
            if (comuna is null)
                return false;

            if (regionId.HasValue && regionId.Value != Guid.Empty && comuna.RegionId != regionId.Value)
                return false;

            if (countryId.HasValue && countryId.Value != Guid.Empty)
            {
                var region = await db.Regions.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == comuna.RegionId, cancellationToken);
                if (region is null || region.CountryId != countryId.Value)
                    return false;
            }

            return true;
        }

        if (regionId is Guid rId && rId != Guid.Empty)
        {
            var region = await db.Regions.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == rId && x.IsActive, cancellationToken);
            if (region is null)
                return false;

            if (countryId.HasValue && countryId.Value != Guid.Empty && region.CountryId != countryId.Value)
                return false;

            return true;
        }

        if (countryId is Guid coId && coId != Guid.Empty)
        {
            return await db.Countries.AsNoTracking()
                .AnyAsync(x => x.Id == coId && x.IsActive, cancellationToken);
        }

        return true;
    }
}
