namespace ComunaClick.SharedUI.Services;

/// <summary>
/// ComunaClic opera en Chile: rechazamos coordenadas cacheadas o erróneas fuera del territorio.
/// </summary>
public static class GeoLocationValidator
{
    private const double ChileMinLat = -56.5;
    private const double ChileMaxLat = -17.0;
    private const double ChileMinLng = -110.0;
    private const double ChileMaxLng = -66.0;

    public static bool IsValidCoordinate(double latitude, double longitude)
        => latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180;

    public static bool IsWithinChile(double latitude, double longitude)
        => IsValidCoordinate(latitude, longitude)
        && latitude >= ChileMinLat && latitude <= ChileMaxLat
        && longitude >= ChileMinLng && longitude <= ChileMaxLng;

    public static bool IsPlausibleSearchOrigin(double latitude, double longitude)
        => IsWithinChile(latitude, longitude);
}
