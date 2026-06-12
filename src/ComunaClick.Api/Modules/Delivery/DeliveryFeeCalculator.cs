using ComunaClick.Api.Configuration;
using ComunaClick.Api.Geo;
using ComunaClick.Api.Modules.Marketplace;
using Microsoft.Extensions.Options;

namespace ComunaClick.Api.Modules.Delivery;

/// <summary>Resultado tipado del cálculo de tarifa de transporte.</summary>
public sealed record DeliveryFeeResult(
    double DistanceKm,
    string AppliedProfile,
    decimal BaseFee,
    decimal PerKmFee,
    int ExcessKm,
    decimal TotalFee);

public interface IDeliveryFeeCalculator
{
    /// <summary>
    /// Calcula la tarifa por distancia y horario. Devuelve null si las
    /// coordenadas no son utilizables (el caller decide el fallback) y lanza
    /// <see cref="DeliveryOutOfRangeException"/> si excede el radio de cobertura.
    /// </summary>
    DeliveryFeeResult? Calculate(
        double? originLat,
        double? originLng,
        double? destinationLat,
        double? destinationLng,
        DateTimeOffset orderTimeUtc);
}

/// <summary>La distancia excede el radio de cobertura configurado: el envío no está disponible.</summary>
public sealed class DeliveryOutOfRangeException : InvalidOperationException
{
    public double DistanceKm { get; }
    public double MaxDistanceKm { get; }

    public DeliveryOutOfRangeException(double distanceKm, double maxDistanceKm)
        : base($"Delivery distance {distanceKm} km exceeds the coverage radius of {maxDistanceKm} km.")
    {
        DistanceKm = distanceKm;
        MaxDistanceKm = maxDistanceKm;
    }
}

public sealed class DeliveryFeeCalculator : IDeliveryFeeCalculator
{
    private readonly DeliveryPricingOptions _options;
    private readonly TimeZoneInfo _timeZone;

    public DeliveryFeeCalculator(IOptions<DeliveryPricingOptions> options)
    {
        _options = options.Value;
        _timeZone = ResolveTimeZone(_options.TimeZone);
    }

    public DeliveryFeeResult? Calculate(
        double? originLat,
        double? originLng,
        double? destinationLat,
        double? destinationLng,
        DateTimeOffset orderTimeUtc)
    {
        if (!_options.Enabled || _options.Profiles.Count == 0)
        {
            return null;
        }

        // Sin ambas coordenadas válidas no hay distancia: el caller aplica el
        // fallback por zona (BaseFee del proveedor / tarifa plana).
        if (originLat is null || originLng is null || destinationLat is null || destinationLng is null
            || !GeoDistance.IsValidCoordinate(originLat.Value, originLng.Value)
            || !GeoDistance.IsValidCoordinate(destinationLat.Value, destinationLng.Value))
        {
            return null;
        }

        var distanceKm = GeoDistance.CalculateDistanceKm(
            originLat.Value, originLng.Value, destinationLat.Value, destinationLng.Value);

        if (_options.MaxDistanceKm > 0 && distanceKm > _options.MaxDistanceKm)
        {
            throw new DeliveryOutOfRangeException(distanceKm, _options.MaxDistanceKm);
        }

        // El perfil (diurno/nocturno/futuras franjas) se decide con la hora
        // LOCAL de Chile, nunca con UTC.
        var localHour = TimeZoneInfo.ConvertTime(orderTimeUtc, _timeZone).Hour;
        var profile = _options.Profiles.FirstOrDefault(p => p.ContainsHour(localHour))
            ?? _options.Profiles[0];

        // Km excedente: ceil — toda fracción de km iniciado se cobra como km
        // entero (decisión de negocio documentada en docs/delivery_tracking.md).
        var excessKm = distanceKm <= profile.BaseRadiusKm
            ? 0
            : (int)Math.Ceiling(distanceKm - profile.BaseRadiusKm);

        var total = FeeCalculator.RoundClp(profile.BaseFee + (excessKm * profile.PerKmFee));
        if (_options.MaxFee > 0 && total > _options.MaxFee)
        {
            total = FeeCalculator.RoundClp(_options.MaxFee);
        }

        return new DeliveryFeeResult(distanceKm, profile.Name, profile.BaseFee, profile.PerKmFee, excessKm, total);
    }

    private static TimeZoneInfo ResolveTimeZone(string id)
    {
        // .NET 8 acepta IDs IANA en Linux y (con ICU) en Windows; el fallback
        // cubre Windows sin ICU usando el ID histórico de Chile continental.
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (TimeZoneNotFoundException)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Pacific SA Standard Time");
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.Utc;
            }
        }
    }
}
