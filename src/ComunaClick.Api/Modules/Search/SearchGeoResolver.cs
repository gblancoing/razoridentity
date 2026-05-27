using ComunaClick.Api.Geo;
using ComunaClick.Api.Persistence.Entities;

namespace ComunaClick.Api.Modules.Search;

internal static class SearchGeoResolver
{
    internal sealed record Coordinates(double Latitude, double Longitude);

    internal static Coordinates? ResolvePartnerCoordinates(Partner partner, IReadOnlyDictionary<Guid, Comuna> comunasById)
    {
        if (partner.Latitude is double lat && partner.Longitude is double lng)
        {
            return new Coordinates(lat, lng);
        }

        if (partner.ComunaId is Guid comunaId && comunasById.TryGetValue(comunaId, out var comuna)
            && comuna.Latitude is double cLat && comuna.Longitude is double cLng)
        {
            return new Coordinates(cLat, cLng);
        }

        return null;
    }

    internal static Coordinates? ResolveServiceCoordinates(Service service, Partner partner, IReadOnlyDictionary<Guid, Comuna> comunasById)
    {
        if (service.Latitude is double lat && service.Longitude is double lng)
        {
            return new Coordinates(lat, lng);
        }

        return ResolvePartnerCoordinates(partner, comunasById);
    }

    internal static Coordinates? ResolveProfessionalCoordinates(Professional professional, IReadOnlyDictionary<Guid, Comuna> comunasById)
    {
        if (professional.ComunaId is Guid comunaId && comunasById.TryGetValue(comunaId, out var comuna)
            && comuna.Latitude is double lat && comuna.Longitude is double lng)
        {
            return new Coordinates(lat, lng);
        }

        return null;
    }

    internal static double? DistanceKmFrom(double originLat, double originLng, Coordinates? target)
    {
        if (target is null)
        {
            return null;
        }

        return GeoDistance.CalculateDistanceKm(originLat, originLng, target.Latitude, target.Longitude);
    }

    internal static bool WithinRadius(double? distanceKm, double maxRadiusKm)
        => distanceKm is null || distanceKm.Value <= maxRadiusKm;
}
