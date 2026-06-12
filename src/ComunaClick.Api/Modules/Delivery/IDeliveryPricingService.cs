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
/// Implementación provisional: tarifa plana de configuración (Delivery:FlatFee)
/// y, si no está definida, el BaseFee del proveedor (comportamiento histórico).
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
