using ComunaClick.Api.Modules.Checkout.Contracts;
using ComunaClick.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ComunaClick.Api.Modules.Delivery;

/// <summary>
/// Cotiza el costo de despacho para un negocio y un destino antes de crear la
/// orden. Resuelve el proveedor por zona (comuna > región > global) y aplica
/// el MISMO IDeliveryPricingService que usa OrderCheckoutService, para que el
/// monto mostrado en el checkout sea el que efectivamente se cobra.
/// </summary>
public interface IDeliveryQuoteService
{
    Task<PublicDeliveryQuoteResponse> GetQuoteAsync(
        Guid partnerId,
        double? destinationLat,
        double? destinationLng,
        CancellationToken cancellationToken = default);
}

public sealed class DeliveryQuoteService : IDeliveryQuoteService
{
    private const string DefaultCurrency = "CLP";

    private readonly CoreDbContext _db;
    private readonly IDeliveryPricingService _pricing;
    private readonly IDeliveryFeeCalculator _calculator;

    public DeliveryQuoteService(
        CoreDbContext db,
        IDeliveryPricingService pricing,
        IDeliveryFeeCalculator calculator)
    {
        _db = db;
        _pricing = pricing;
        _calculator = calculator;
    }

    public async Task<PublicDeliveryQuoteResponse> GetQuoteAsync(
        Guid partnerId,
        double? destinationLat,
        double? destinationLng,
        CancellationToken cancellationToken = default)
    {
        var partner = await _db.Partners.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == partnerId && x.IsVisible, cancellationToken);

        if (partner is null)
        {
            return NotAvailable("Partner not found.");
        }

        // Mismo filtro y orden que DeliveryProvidersController.ListByPartner:
        // proveedor de la comuna del negocio, si no de su región, si no global.
        var provider = await _db.DeliveryProviders.AsNoTracking()
            .Where(x =>
                x.IsActive &&
                (!x.TenantId.HasValue || x.TenantId.Value == partner.TenantId) &&
                (
                    (x.ComunaId.HasValue && partner.ComunaId.HasValue && x.ComunaId.Value == partner.ComunaId.Value) ||
                    (!x.ComunaId.HasValue && x.RegionId.HasValue && partner.RegionId.HasValue && x.RegionId.Value == partner.RegionId.Value) ||
                    (!x.ComunaId.HasValue && !x.RegionId.HasValue)
                ))
            .OrderByDescending(x => x.ComunaId.HasValue)
            .ThenByDescending(x => x.RegionId.HasValue)
            .ThenBy(x => x.Name)
            .FirstOrDefaultAsync(cancellationToken);

        if (provider is null)
        {
            // Sin proveedor en la zona no se cobra envío (mismo contrato que
            // el checkout cuando DeliveryProviderId llega null).
            return new PublicDeliveryQuoteResponse(
                Available: true,
                FeeApplies: false,
                Fee: 0m,
                Currency: DefaultCurrency,
                DistanceKm: null,
                ProfileName: null,
                DeliveryProviderId: null,
                DeliveryProviderName: null,
                OutOfRange: false,
                MaxDistanceKm: null,
                Message: null);
        }

        decimal fee;
        try
        {
            fee = await _pricing.GetDeliveryFeeAsync(
                new DeliveryPricingContext(
                    provider,
                    partner.Latitude,
                    partner.Longitude,
                    destinationLat,
                    destinationLng),
                cancellationToken);
        }
        catch (DeliveryOutOfRangeException ex)
        {
            return new PublicDeliveryQuoteResponse(
                Available: true,
                FeeApplies: true,
                Fee: 0m,
                Currency: DefaultCurrency,
                DistanceKm: ex.DistanceKm,
                ProfileName: null,
                DeliveryProviderId: provider.Id,
                DeliveryProviderName: provider.Name,
                OutOfRange: true,
                MaxDistanceKm: ex.MaxDistanceKm,
                Message: $"Delivery distance {ex.DistanceKm:0.#} km exceeds the {ex.MaxDistanceKm:0.#} km coverage radius.");
        }

        // Detalle informativo (km/perfil); null cuando aplicó el fallback plano.
        DeliveryFeeResult? detail = null;
        try
        {
            detail = _calculator.Calculate(
                partner.Latitude,
                partner.Longitude,
                destinationLat,
                destinationLng,
                DateTimeOffset.UtcNow);
        }
        catch (DeliveryOutOfRangeException)
        {
            // Ya cubierto arriba; el detalle es cosmético.
        }

        return new PublicDeliveryQuoteResponse(
            Available: true,
            FeeApplies: true,
            Fee: fee,
            Currency: DefaultCurrency,
            DistanceKm: detail?.DistanceKm,
            ProfileName: detail?.AppliedProfile,
            DeliveryProviderId: provider.Id,
            DeliveryProviderName: provider.Name,
            OutOfRange: false,
            MaxDistanceKm: null,
            Message: null);
    }

    private static PublicDeliveryQuoteResponse NotAvailable(string message) => new(
        Available: false,
        FeeApplies: false,
        Fee: 0m,
        Currency: DefaultCurrency,
        DistanceKm: null,
        ProfileName: null,
        DeliveryProviderId: null,
        DeliveryProviderName: null,
        OutOfRange: false,
        MaxDistanceKm: null,
        Message: message);
}
