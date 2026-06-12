using ComunaClick.Api.Persistence;
using ComunaClick.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace ComunaClick.Api.Modules.Delivery;

/// <summary>Desglose del slip de liquidación del transporte.</summary>
public sealed record DeliverySettlementResponse(
    Guid Id,
    Guid OrderId,
    Guid PartnerId,
    Guid? CourierId,
    string? CourierName,
    decimal GrossAmount,
    decimal PlatformFeeAmount,
    decimal? MercadoPagoFeeAmount,
    decimal NetToCourierAmount,
    string Currency,
    string Status,
    DateTimeOffset? SettledAt,
    string? Notes,
    DateTimeOffset CreatedAt);

/// <summary>
/// Consulta y liquidación de slips del transporte. Autorización:
/// el negocio ve los de sus pedidos, el admin de plataforma ve todos y marca
/// liquidado, y el transportista consulta el suyo con su link/token por pedido.
/// </summary>
[ApiController]
public sealed class DeliverySettlementsController : ControllerBase
{
    private readonly CoreDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IDeliveryCourierTokenService _courierTokens;
    private readonly IDeliverySettlementService _settlements;

    public DeliverySettlementsController(
        CoreDbContext db,
        ITenantContext tenantContext,
        IDeliveryCourierTokenService courierTokens,
        IDeliverySettlementService settlements)
    {
        _db = db;
        _tenantContext = tenantContext;
        _courierTokens = courierTokens;
        _settlements = settlements;
    }

    [Authorize(Policy = "partner.staff")]
    [HttpGet("/v1/partners/{partnerId:guid}/delivery-settlements")]
    public async Task<ActionResult<IReadOnlyList<DeliverySettlementResponse>>> ListByPartner(Guid partnerId)
    {
        var access = await PartnerAccessAuthorization.EnsurePartnerAccessAsync(
            _db, User, partnerId, _tenantContext.TenantId, _tenantContext.PartnerId, HttpContext.RequestAborted);
        if (access != PartnerAccessResult.Allowed)
        {
            return Forbid();
        }

        var items = await QuerySettlements()
            .Where(x => x.Settlement.PartnerId == partnerId)
            .Take(200)
            .ToListAsync(HttpContext.RequestAborted);

        return Ok(items.Select(ToResponse).ToList());
    }

    [Authorize(Policy = "platform.admin")]
    [HttpGet("/v1/admin/delivery-settlements")]
    public async Task<ActionResult<IReadOnlyList<DeliverySettlementResponse>>> ListAll([FromQuery] string? status)
    {
        var query = QuerySettlements();
        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalized = status.Trim().ToLowerInvariant();
            query = query.Where(x => x.Settlement.Status == normalized);
        }

        var items = await query.Take(500).ToListAsync(HttpContext.RequestAborted);
        return Ok(items.Select(ToResponse).ToList());
    }

    /// <summary>Marca el slip como liquidado (la transferencia al transportista ya se efectuó).</summary>
    [Authorize(Policy = "platform.admin")]
    [HttpPost("/v1/admin/delivery-settlements/{id:guid}/settle")]
    public async Task<IActionResult> Settle(Guid id, [FromBody] DeliverySettlementSettleRequest? request)
    {
        var (ok, error) = await _settlements.MarkSettledAsync(id, request?.Notes, HttpContext.RequestAborted);
        return ok ? Ok(new { ok = true }) : BadRequest(new { message = error });
    }

    /// <summary>
    /// El transportista consulta su propio slip con el token del reparto
    /// (mismo link seguro del pedido; no requiere cuenta de usuario).
    /// </summary>
    [AllowAnonymous]
    [EnableRateLimiting("public-read")]
    [HttpGet("/api/courier/settlement")]
    public async Task<ActionResult<DeliverySettlementResponse>> GetOwn([FromQuery] Guid orderId, [FromQuery] string token)
    {
        var order = await _db.Orders.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == orderId, HttpContext.RequestAborted);
        if (order is null || !_courierTokens.TryValidate(token, orderId, order.CourierTokenKey))
        {
            return NotFound();
        }

        var item = await QuerySettlements()
            .FirstOrDefaultAsync(x => x.Settlement.OrderId == orderId, HttpContext.RequestAborted);
        return item is null ? NotFound() : Ok(ToResponse(item));
    }

    private IQueryable<SettlementRow> QuerySettlements()
        => _db.DeliverySettlements.AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new SettlementRow(
                x,
                _db.Couriers.IgnoreQueryFilters()
                    .Where(c => c.Id == x.CourierId)
                    .Select(c => c.Name)
                    .FirstOrDefault()));

    private static DeliverySettlementResponse ToResponse(SettlementRow row)
        => new(
            row.Settlement.Id,
            row.Settlement.OrderId,
            row.Settlement.PartnerId,
            row.Settlement.CourierId,
            row.CourierName,
            row.Settlement.GrossAmount,
            row.Settlement.PlatformFeeAmount,
            row.Settlement.MercadoPagoFeeAmount,
            row.Settlement.NetToCourierAmount,
            row.Settlement.Currency,
            row.Settlement.Status,
            row.Settlement.SettledAt,
            row.Settlement.Notes,
            row.Settlement.CreatedAt);

    private sealed record SettlementRow(Persistence.Entities.DeliverySettlement Settlement, string? CourierName);
}

public sealed record DeliverySettlementSettleRequest(string? Notes);
