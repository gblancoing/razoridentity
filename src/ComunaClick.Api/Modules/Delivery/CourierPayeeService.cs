using ComunaClick.Api.Persistence;
using ComunaClick.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ComunaClick.Api.Modules.Delivery;

/// <summary>Estado de la cuenta de cobro del transportista.</summary>
public sealed record CourierPayeeStatus(
    Guid CourierId,
    bool HasSeller,
    string ConnectionStatus,
    bool CheckoutReady,
    string? MpUserId,
    DateTimeOffset? ConnectedAt);

public interface ICourierPayeeService
{
    /// <summary>Garantiza la fila Seller del transportista para reutilizar el OAuth de MercadoPago.</summary>
    Task<Seller> EnsurePayeeSellerAsync(Courier courier, CancellationToken cancellationToken = default);

    Task<CourierPayeeStatus> GetStatusAsync(Guid courierId, CancellationToken cancellationToken = default);
}

/// <summary>
/// El transportista es un prestador de servicio con cuenta MercadoPago propia,
/// análogo a un comercio vendedor. Decisión de modelado: se le crea una fila
/// <see cref="Seller"/> con Id = Courier.Id (mismo patrón Id-compartido que
/// Partner→Seller), lo que reutiliza COMPLETO el flujo OAuth, el cifrado de
/// tokens y el estado "checkout ready" existentes sin duplicar lógica.
/// </summary>
public sealed class CourierPayeeService : ICourierPayeeService
{
    private readonly CoreDbContext _db;

    public CourierPayeeService(CoreDbContext db)
    {
        _db = db;
    }

    public async Task<Seller> EnsurePayeeSellerAsync(Courier courier, CancellationToken cancellationToken = default)
    {
        var seller = await _db.Sellers
            .Include(x => x.MercadoPagoAccount)
            .FirstOrDefaultAsync(x => x.Id == courier.Id, cancellationToken);
        if (seller is not null)
        {
            return seller;
        }

        seller = new Seller
        {
            Id = courier.Id,
            TenantId = courier.TenantId,
            Name = courier.Name,
            // Email sintético: el courier opera por teléfono/WhatsApp, sin cuenta de usuario.
            Email = $"courier-{courier.Id:N}@comunaclic.cl",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _db.Sellers.Add(seller);
        await _db.SaveChangesAsync(cancellationToken);
        return seller;
    }

    public async Task<CourierPayeeStatus> GetStatusAsync(Guid courierId, CancellationToken cancellationToken = default)
    {
        var seller = await _db.Sellers.AsNoTracking()
            .Include(x => x.MercadoPagoAccount)
            .FirstOrDefaultAsync(x => x.Id == courierId, cancellationToken);

        var account = seller?.MercadoPagoAccount;
        var connected = string.Equals(account?.ConnectionStatus, "connected", StringComparison.OrdinalIgnoreCase);
        return new CourierPayeeStatus(
            courierId,
            seller is not null,
            account?.ConnectionStatus ?? "disconnected",
            connected && !string.IsNullOrWhiteSpace(account?.AccessTokenEncrypted),
            account?.MpUserId,
            account?.ConnectedAt);
    }
}
