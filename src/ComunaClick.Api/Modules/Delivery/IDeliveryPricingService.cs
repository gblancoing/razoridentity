using ComunaClick.Api.Configuration;
using ComunaClick.Api.Persistence.Entities;
using Microsoft.Extensions.Options;

namespace ComunaClick.Api.Modules.Delivery;

/// <summary>Contexto disponible para calcular el costo del despacho.</summary>
public sealed record DeliveryPricingContext(
    DeliveryProvider? Provider,
    double? OriginLat,
    double? OriginLng,
    double? DestinationLat,
    double? DestinationLng);

/// <summary>
/// Punto de extensión del costo de envío. La implementación real por
/// distancia/kilómetro se definirá en una tarea posterior (el contexto ya
/// trae origen y destino para esa fórmula).
/// </summary>
public interface IDeliveryPricingService
{
    Task<decimal> GetDeliveryFeeAsync(DeliveryPricingContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementación plana: tarifa de configuración (Delivery:FlatFee) y, si no
/// está definida, el BaseFee del proveedor (comportamiento histórico). Hoy es
/// el fallback del cálculo dinámico cuando faltan coordenadas.
/// </summary>
public sealed class FlatRateDeliveryPricingService : IDeliveryPricingService
{
    private readonly DeliveryOptions _options;

    public FlatRateDeliveryPricingService(IOptions<DeliveryOptions> options)
    {
        _options = options.Value;
    }

    public Task<decimal> GetDeliveryFeeAsync(DeliveryPricingContext context, CancellationToken cancellationToken = default)
    {
        if (context.Provider is null)
        {
            // Sin proveedor de despacho no se cobra envío (retiro / acuerdo directo).
            return Task.FromResult(0m);
        }

        var fee = _options.FlatFee is > 0 ? _options.FlatFee.Value : context.Provider.BaseFee;
        return Task.FromResult(fee);
    }
}

/// <summary>
/// Tarifa dinámica por distancia (GeoDistance) y horario chileno
/// (DeliveryPricing en appsettings). Si faltan coordenadas válidas cae al
/// fallback plano por zona; si la distancia excede el radio de cobertura
/// propaga <see cref="DeliveryOutOfRangeException"/> (el checkout rechaza
/// la compra con mensaje claro).
/// </summary>
public sealed class DynamicDeliveryPricingService : IDeliveryPricingService
{
    private readonly IDeliveryFeeCalculator _calculator;
    private readonly FlatRateDeliveryPricingService _flatFallback;

    public DynamicDeliveryPricingService(IDeliveryFeeCalculator calculator, IOptions<DeliveryOptions> options)
    {
        _calculator = calculator;
        _flatFallback = new FlatRateDeliveryPricingService(options);
    }

    public async Task<decimal> GetDeliveryFeeAsync(DeliveryPricingContext context, CancellationToken cancellationToken = default)
    {
        if (context.Provider is null)
        {
            // Mismo contrato histórico: sin proveedor de despacho no se cobra
            // envío en el checkout (retiro o "envío por pagar" en efectivo).
            return 0m;
        }

        var result = _calculator.Calculate(
            context.OriginLat,
            context.OriginLng,
            context.DestinationLat,
            context.DestinationLng,
            DateTimeOffset.UtcNow);

        return result?.TotalFee
            ?? await _flatFallback.GetDeliveryFeeAsync(context, cancellationToken);
    }
}
