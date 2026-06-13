using System.Linq.Expressions;
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
    private readonly IDeliverySettlementPaymentService _settlementPayments;

    public DeliverySettlementsController(
        CoreDbContext db,
        ITenantContext tenantContext,
        IDeliveryCourierTokenService courierTokens,
        IDeliverySettlementService settlements,
        IDeliverySettlementPaymentService settlementPayments)
    {
        _db = db;
        _tenantContext = tenantContext;
        _courierTokens = courierTokens;
        _settlements = settlements;
        _settlementPayments = settlementPayments;
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

        var items = await QuerySettlements(x => x.PartnerId == partnerId)
            .Take(200)
            .ToListAsync(HttpContext.RequestAborted);

        return Ok(items.Select(ToResponse).ToList());
    }

    [Authorize(Policy = "platform.admin")]
    [HttpGet("/v1/admin/delivery-settlements")]
    public async Task<ActionResult<IReadOnlyList<DeliverySettlementResponse>>> ListAll([FromQuery] string? status)
    {
        Expression<Func<Persistence.Entities.DeliverySettlement, bool>>? predicate = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalized = status.Trim().ToLowerInvariant();
            predicate = x => x.Status == normalized;
        }

        var items = await QuerySettlements(predicate).Take(500).ToListAsync(HttpContext.RequestAborted);
        return Ok(items.Select(ToResponse).ToList());
    }

    /// <summary>
    /// Genera el link MercadoPago con el que el comercio paga el envío al
    /// repartidor tras la entrega (collector = cuenta MP del repartidor).
    /// </summary>
    [Authorize(Policy = "partner.staff")]
    [HttpPost("/v1/partners/{partnerId:guid}/delivery-settlements/{id:guid}/pay")]
    public async Task<IActionResult> Pay(Guid partnerId, Guid id)
    {
        var access = await PartnerAccessAuthorization.EnsurePartnerAccessAsync(
            _db, User, partnerId, _tenantContext.TenantId, _tenantContext.PartnerId, HttpContext.RequestAborted);
        if (access != PartnerAccessResult.Allowed)
        {
            return Forbid();
        }

        var (link, error) = await _settlementPayments.CreatePaymentLinkAsync(id, partnerId, HttpContext.RequestAborted);
        return link is null
            ? BadRequest(new { message = error })
            : Ok(new { initPoint = link.InitPoint, status = link.Status });
    }

    /// <summary>
    /// El comercio declara que pagó al transportista en efectivo o por transferencia bancaria.
    /// Deja el slip en "manual_confirming" hasta que el transportista confirme el recibo.
    /// </summary>
    [Authorize(Policy = "partner.staff")]
    [HttpPost("/v1/partners/{partnerId:guid}/delivery-settlements/{id:guid}/pay-manual")]
    public async Task<IActionResult> PayManual(Guid partnerId, Guid id, [FromBody] DeliverySettlementManualPayRequest request)
    {
        var access = await PartnerAccessAuthorization.EnsurePartnerAccessAsync(
            _db, User, partnerId, _tenantContext.TenantId, _tenantContext.PartnerId, HttpContext.RequestAborted);
        if (access != PartnerAccessResult.Allowed)
        {
            return Forbid();
        }

        var (ok, error) = await _settlements.DeclareManualPaymentAsync(id, partnerId, request.Method, request.Notes, HttpContext.RequestAborted);
        return ok ? Ok(new { ok = true, status = "manual_confirming" }) : BadRequest(new { message = error });
    }

    /// <summary>
    /// Devuelve la liquidación existente para un pedido (sin crearla).
    /// 404 si aún no existe.
    /// </summary>
    [Authorize(Policy = "partner.staff")]
    [HttpGet("/v1/partners/{partnerId:guid}/orders/{orderId:guid}/settlement")]
    public async Task<ActionResult<DeliverySettlementResponse>> GetSettlement(Guid partnerId, Guid orderId)
    {
        var access = await PartnerAccessAuthorization.EnsurePartnerAccessAsync(
            _db, User, partnerId, _tenantContext.TenantId, _tenantContext.PartnerId, HttpContext.RequestAborted);
        if (access != PartnerAccessResult.Allowed)
        {
            return Forbid();
        }

        var item = await QuerySettlements(x => x.OrderId == orderId && x.PartnerId == partnerId)
            .FirstOrDefaultAsync(HttpContext.RequestAborted);

        return item is null ? NotFound() : Ok(ToResponse(item));
    }

    /// <summary>
    /// Crea (o devuelve el existente) el slip de liquidación de envío para un pedido
    /// pagado con delivery. Permite al comercio registrar manualmente una liquidación
    /// cuando no fue generada automáticamente (ej. webhook de MP no disparó).
    /// </summary>
    [Authorize(Policy = "partner.staff")]
    [HttpPost("/v1/partners/{partnerId:guid}/orders/{orderId:guid}/settlement")]
    public async Task<ActionResult<DeliverySettlementResponse>> EnsureSettlement(Guid partnerId, Guid orderId)
    {
        var access = await PartnerAccessAuthorization.EnsurePartnerAccessAsync(
            _db, User, partnerId, _tenantContext.TenantId, _tenantContext.PartnerId, HttpContext.RequestAborted);
        if (access != PartnerAccessResult.Allowed)
        {
            return Forbid();
        }

        var order = await _db.Orders.AsNoTracking()
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == orderId && x.PartnerId == partnerId, HttpContext.RequestAborted);
        if (order is null)
        {
            return NotFound(new { message = "Pedido no encontrado." });
        }

        if (!string.Equals(order.DeliveryType, "delivery", StringComparison.OrdinalIgnoreCase) || order.DeliveryFee <= 0)
        {
            return BadRequest(new { message = "Este pedido no tiene costo de envío liquidable." });
        }

        var settlement = await _settlements.CreateForPaidOrderAsync(order, payment: null, HttpContext.RequestAborted);
        if (settlement is null)
        {
            return BadRequest(new { message = "No se pudo crear la liquidación. Verifica que el pedido esté pagado y tenga delivery." });
        }

        var courierName = settlement.CourierId.HasValue
            ? await _db.Couriers.IgnoreQueryFilters()
                .Where(c => c.Id == settlement.CourierId.Value)
                .Select(c => c.Name)
                .FirstOrDefaultAsync(HttpContext.RequestAborted)
            : null;

        return Ok(ToResponse(new SettlementRow(settlement, courierName)));
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

        var item = await QuerySettlements(x => x.OrderId == orderId)
            .FirstOrDefaultAsync(HttpContext.RequestAborted);
        return item is null ? NotFound() : Ok(ToResponse(item));
    }

    private IQueryable<SettlementRow> QuerySettlements(
        Expression<Func<Persistence.Entities.DeliverySettlement, bool>>? predicate = null)
    {
        var q = _db.DeliverySettlements.AsNoTracking();
        if (predicate is not null) q = q.Where(predicate);
        return q.OrderByDescending(x => x.CreatedAt)
            .Select(x => new SettlementRow(
                x,
                _db.Couriers.IgnoreQueryFilters()
                    .Where(c => c.Id == x.CourierId)
                    .Select(c => c.Name)
                    .FirstOrDefault()));
    }

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

public sealed record DeliverySettlementManualPayRequest(string Method, string? Notes = null);
