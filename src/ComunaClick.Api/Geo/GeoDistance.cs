namespace ComunaClick.Api.Geo;

public static class GeoDistance
{
    public static double CalculateDistanceKm(double latitude1, double longitude1, double latitude2, double longitude2)
    {
        const double earthRadiusKm = 6371d;

        var dLatitude = DegreesToRadians(latitude2 - latitude1);
        var dLongitude = DegreesToRadians(longitude2 - longitude1);
        var lat1 = DegreesToRadians(latitude1);
        var lat2 = DegreesToRadians(latitude2);

        var a = Math.Sin(dLatitude / 2) * Math.Sin(dLatitude / 2)
            + Math.Cos(lat1) * Math.Cos(lat2)
            * Math.Sin(dLongitude / 2) * Math.Sin(dLongitude / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return Math.Round(earthRadiusKm * c, 1, MidpointRounding.AwayFromZero);
    }

    public static bool IsValidCoordinate(double latitude, double longitude)
        => latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180;

    private static double DegreesToRadians(double degrees) => degrees * (Math.PI / 180d);
}
