using ComunaClick.Shared.Api.Buyer;
using Microsoft.JSInterop;

namespace ComunaClick.SharedUI.Services;

/// <summary>
/// Ubicación GPS actual del visitante para mapas y búsquedas por proximidad.
/// </summary>
public sealed class UserMapLocationService
{
    private readonly IJSRuntime _jsRuntime;
    private readonly LocaleService _locale;

    public UserMapLocationService(IJSRuntime jsRuntime, LocaleService locale)
    {
        _jsRuntime = jsRuntime;
        _locale = locale;
    }

    /// <summary>
    /// Obtiene la ubicación del dispositivo (siempre solicitud fresca al GPS).
    /// </summary>
    public async Task<UserMapLocationResult> GetCurrentAsync()
    {
        try
        {
            var position = await _jsRuntime.InvokeAsync<BrowserLocationDto>(
                "comunaclic.getCurrentPosition",
                new { forceFresh = true, timeout = 20000 });

            if (GeoLocationValidator.IsPlausibleSearchOrigin(position.Latitude, position.Longitude))
            {
                return new UserMapLocationResult(position.Latitude, position.Longitude, FromDevice: true, null);
            }

            return new UserMapLocationResult(
                0,
                0,
                FromDevice: false,
                _locale.T("home.search.location.outOfChile"));
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var message = string.IsNullOrWhiteSpace(ex.Message)
                ? _locale.T("home.search.location.denied")
                : ex.Message;
            return new UserMapLocationResult(0, 0, FromDevice: false, message);
        }
    }

    public static object ToMapPayload(UserMapLocationResult location)
        => new { latitude = location.Latitude, longitude = location.Longitude };

    public static GeoFilter? ToGeoFilter(UserMapLocationResult location, double? radiusKm = null)
        => location.FromDevice
            ? new GeoFilter(Latitude: location.Latitude, Longitude: location.Longitude, RadiusKm: radiusKm ?? 30)
            : null;

    private sealed class BrowserLocationDto
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }
}

public readonly record struct UserMapLocationResult(
    double Latitude,
    double Longitude,
    bool FromDevice,
    string? ErrorMessage);
