using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ComunaClick.Api.Persistence;
using ComunaClick.Api.Persistence.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ComunaClick.Api.Modules.Delivery;

public sealed record CourierMembershipResponse(
    Guid CourierId,
    Guid PartnerId,
    string PartnerName,
    string Name,
    string Phone,
    string? Company,
    string Kind,
    bool IsAvailable,
    string? Email,
    CourierPayeeStatus Payee);

public sealed record CourierMeResponse(
    IReadOnlyList<CourierMembershipResponse> Items,
    string? Email);

public sealed record CourierTripResponse(
    Guid OrderId,
    string OrderShortId,
    string PartnerName,
    string? DeliveryStatus,
    string? DeliveryAddress,
    string? OriginAddress,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    decimal? NetAmount,
    string? SettlementStatus,
    string Currency);

public sealed record CourierTripsPageResponse(
    IReadOnlyList<CourierTripResponse> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record CourierEarningsItemResponse(
    Guid OrderId,
    string OrderShortId,
    DateTimeOffset CreatedAt,
    decimal NetAmount,
    string Status);

public sealed record CourierEarningsResponse(
    decimal SettledTotal,
    decimal PendingTotal,
    decimal ManualTotal,
    int SettledCount,
    int PendingCount,
    string Currency,
    IReadOnlyList<CourierEarningsItemResponse> Recent);

public sealed record CourierAvailabilityRequest(bool IsAvailable);

/// <summary>
/// Portal del repartidor con cuenta. La identidad se resuelve igual que el
/// perfil profesional: por el claim de email del usuario autenticado (y por
/// user_id una vez reclamado el perfil). El comercio registra al repartidor
/// con su correo; al primer ingreso con ese correo el perfil queda vinculado.
/// </summary>
[ApiController]
[Authorize(Policy = "buyer.profile")]
[Route("v1/courier/me")]
public sealed class CourierPortalController : ControllerBase
{
    private readonly CoreDbContext _db;
    private readonly ICourierPayeeService _payeeService;
    private readonly Marketplace.MercadoPagoOAuthService _mpOAuth;

    public CourierPortalController(
        CoreDbContext db,
        ICourierPayeeService payeeService,
        Marketplace.MercadoPagoOAuthService mpOAuth)
    {
        _db = db;
        _payeeService = payeeService;
        _mpOAuth = mpOAuth;
    }

    [HttpGet]
    public async Task<ActionResult<CourierMeResponse>> Get(CancellationToken cancellationToken)
    {
        var couriers = await ResolveMyCouriersAsync(cancellationToken);
        var partnerIds = couriers.Select(x => x.PartnerId).Distinct().ToList();
        var partnerNames = await _db.Partners.IgnoreQueryFilters().AsNoTracking()
            .Where(x => partnerIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);

        var items = new List<CourierMembershipResponse>(couriers.Count);
        foreach (var courier in couriers)
        {
            var payee = await _payeeService.GetStatusAsync(courier.Id, cancellationToken);
            items.Add(new CourierMembershipResponse(
                courier.Id,
                courier.PartnerId,
                partnerNames.TryGetValue(courier.PartnerId, out var name) ? name : "Negocio",
                courier.Name,
                courier.Phone,
                courier.Company,
                courier.Kind,
                courier.IsAvailable,
                courier.Email,
                payee));
        }

        return Ok(new CourierMeResponse(items, ResolveEmail(User)));
    }

    [HttpGet("trips")]
    public async Task<ActionResult<CourierTripsPageResponse>> GetTrips(
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var myIds = (await ResolveMyCouriersAsync(cancellationToken)).Select(x => x.Id).ToList();
        if (myIds.Count == 0)
        {
            return Ok(new CourierTripsPageResponse(Array.Empty<CourierTripResponse>(), page, pageSize, 0));
        }

        var query = _db.Orders.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.CourierId.HasValue && myIds.Contains(x.CourierId.Value));

        // activos = en curso; completados = entregados.
        var normalized = status?.Trim().ToLowerInvariant();
        query = normalized switch
        {
            "activos" => query.Where(x =>
                x.DeliveryStatus == DeliveryStatuses.Assigned
                || x.DeliveryStatus == DeliveryStatuses.PickedUp
                || x.DeliveryStatus == DeliveryStatuses.InTransit),
            "completados" => query.Where(x => x.DeliveryStatus == DeliveryStatuses.Delivered),
            _ => query
        };

        var totalCount = await query.CountAsync(cancellationToken);
        var orders = await query
            .OrderByDescending(x => x.UpdatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var orderIds = orders.Select(x => x.Id).ToList();
        var settlements = await _db.DeliverySettlements.IgnoreQueryFilters().AsNoTracking()
            .Where(x => orderIds.Contains(x.OrderId))
            .ToDictionaryAsync(x => x.OrderId, cancellationToken);
        var partnerIds = orders.Select(x => x.PartnerId).Distinct().ToList();
        var partnerNames = await _db.Partners.IgnoreQueryFilters().AsNoTracking()
            .Where(x => partnerIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);

        var items = orders.Select(order =>
        {
            settlements.TryGetValue(order.Id, out var settlement);
            return new CourierTripResponse(
                order.Id,
                order.Id.ToString()[..8],
                partnerNames.TryGetValue(order.PartnerId, out var name) ? name : "Negocio",
                order.DeliveryStatus,
                order.DeliveryAddress,
                order.OriginAddress,
                order.CreatedAt,
                order.UpdatedAt,
                settlement?.NetToCourierAmount,
                settlement?.Status,
                settlement?.Currency ?? order.Currency ?? "CLP");
        }).ToList();

        return Ok(new CourierTripsPageResponse(items, page, pageSize, totalCount));
    }

    [HttpGet("earnings")]
    public async Task<ActionResult<CourierEarningsResponse>> GetEarnings(CancellationToken cancellationToken)
    {
        var myIds = (await ResolveMyCouriersAsync(cancellationToken)).Select(x => x.Id).ToList();
        if (myIds.Count == 0)
        {
            return Ok(new CourierEarningsResponse(0m, 0m, 0m, 0, 0, "CLP", Array.Empty<CourierEarningsItemResponse>()));
        }

        var settlements = await _db.DeliverySettlements.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.CourierId.HasValue && myIds.Contains(x.CourierId.Value))
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        // "processing" cuenta como pendiente (link de pago emitido, sin aprobar).
        var settled = settlements.Where(x => x.Status == "settled").ToList();
        var pending = settlements.Where(x => x.Status is "pending" or "processing").ToList();
        var manual = settlements.Where(x => x.Status == "manual").ToList();

        var recent = settlements.Take(10)
            .Select(x => new CourierEarningsItemResponse(
                x.OrderId,
                x.OrderId.ToString()[..8],
                x.CreatedAt,
                x.NetToCourierAmount,
                x.Status))
            .ToList();

        return Ok(new CourierEarningsResponse(
            settled.Sum(x => x.NetToCourierAmount),
            pending.Sum(x => x.NetToCourierAmount),
            manual.Sum(x => x.NetToCourierAmount),
            settled.Count,
            pending.Count + manual.Count,
            settlements.FirstOrDefault()?.Currency ?? "CLP",
            recent));
    }

    /// <summary>Autoservicio: el repartidor vincula SU cuenta MercadoPago para recibir los pagos de envío.</summary>
    [HttpPost("{courierId:guid}/mercadopago/start")]
    public async Task<IActionResult> StartMercadoPago(Guid courierId, CancellationToken cancellationToken)
    {
        var courier = (await ResolveMyCouriersAsync(cancellationToken))
            .FirstOrDefault(x => x.Id == courierId);
        if (courier is null)
        {
            return NotFound();
        }

        await _payeeService.EnsurePayeeSellerAsync(courier, cancellationToken);
        var start = await _mpOAuth.StartAsync(courier.Id, cancellationToken);
        return Ok(new { courierId = courier.Id, authorizationUrl = start.AuthorizationUrl });
    }

    /// <summary>
    /// Desvincula la cuenta MercadoPago del repartidor (ej. se vinculó una
    /// cuenta equivocada). Idempotente: sin cuenta conectada responde ok igual,
    /// dejando el camino libre para vincular la correcta.
    /// </summary>
    [HttpPost("{courierId:guid}/mercadopago/disconnect")]
    public async Task<IActionResult> DisconnectMercadoPago(Guid courierId, CancellationToken cancellationToken)
    {
        var mine = await ResolveMyCouriersAsync(cancellationToken);
        if (mine.All(x => x.Id != courierId))
        {
            return NotFound();
        }

        var status = await _payeeService.GetStatusAsync(courierId, cancellationToken);
        if (status.HasSeller && !string.Equals(status.ConnectionStatus, "disconnected", StringComparison.OrdinalIgnoreCase))
        {
            await _mpOAuth.DisconnectAsync(courierId, cancellationToken);
        }

        return Ok(new { courierId, disconnected = true });
    }

    /// <summary>El repartidor se marca disponible / no disponible.</summary>
    [HttpPatch("{courierId:guid}")]
    public async Task<IActionResult> UpdateAvailability(Guid courierId, CourierAvailabilityRequest request, CancellationToken cancellationToken)
    {
        var mine = await ResolveMyCouriersAsync(cancellationToken);
        if (mine.All(x => x.Id != courierId))
        {
            return NotFound();
        }

        var entity = await _db.Couriers.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == courierId, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        entity.IsAvailable = request.IsAvailable;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { courierId, isAvailable = entity.IsAvailable });
    }

    /// <summary>
    /// Perfiles de repartidor del usuario autenticado: por user_id reclamado o
    /// por coincidencia de email; al primer match por email se persiste el
    /// user_id (reclamo del perfil).
    /// </summary>
    private async Task<List<Courier>> ResolveMyCouriersAsync(CancellationToken cancellationToken)
    {
        var userId = ResolveUserId(User);
        var email = ResolveEmail(User)?.Trim().ToLowerInvariant();
        if (userId is null && string.IsNullOrWhiteSpace(email))
        {
            return new List<Courier>();
        }

        var couriers = await _db.Couriers.IgnoreQueryFilters()
            .Where(x =>
                (userId != null && x.UserId == userId) ||
                (email != null && x.Email != null && x.Email.ToLower() == email))
            .ToListAsync(cancellationToken);

        var claimed = false;
        if (userId is not null)
        {
            foreach (var courier in couriers.Where(x => x.UserId is null))
            {
                courier.UserId = userId;
                courier.UpdatedAt = DateTimeOffset.UtcNow;
                claimed = true;
            }
        }

        if (claimed)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }

        return couriers;
    }

    private static string? ResolveEmail(ClaimsPrincipal user)
        => user.FindFirst(ClaimTypes.Email)?.Value
           ?? user.FindFirst(JwtRegisteredClaimNames.Email)?.Value
           ?? user.FindFirst("email")?.Value;

    private static Guid? ResolveUserId(ClaimsPrincipal user)
    {
        var raw = user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(raw, out var id) ? id : null;
    }
}
