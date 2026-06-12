using ComunaClick.Api.Modules.Delivery.Contracts;
using ComunaClick.Api.Persistence;
using ComunaClick.Api.Persistence.Entities;
using ComunaClick.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ComunaClick.Api.Modules.Delivery;

/// <summary>
/// Gestión de repartidores propios del negocio + asignación a pedidos.
/// Un negocio puede tener varios repartidores, incluso de empresas distintas.
/// </summary>
[ApiController]
[Authorize(Policy = "partner.staff")]
public sealed class CouriersController : ControllerBase
{
    private readonly CoreDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IDeliveryService _deliveryService;

    public CouriersController(CoreDbContext db, ITenantContext tenantContext, IDeliveryService deliveryService)
    {
        _db = db;
        _tenantContext = tenantContext;
        _deliveryService = deliveryService;
    }

    [HttpGet("/v1/partners/{partnerId:guid}/couriers")]
    public async Task<ActionResult<IReadOnlyList<CourierResponse>>> List(Guid partnerId)
    {
        if (!await CanManagePartnerAsync(partnerId))
        {
            return Forbid();
        }

        var items = await _db.Couriers.AsNoTracking()
            .Where(x => x.PartnerId == partnerId)
            .OrderBy(x => x.Name)
            .Select(x => new CourierResponse(x.Id, x.PartnerId, x.Name, x.Phone, x.Company, x.IsAvailable))
            .ToListAsync();

        return Ok(items);
    }

    [HttpPost("/v1/partners/{partnerId:guid}/couriers")]
    public async Task<ActionResult<CourierResponse>> Create(Guid partnerId, CourierCreateRequest request)
    {
        if (!await CanManagePartnerAsync(partnerId))
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length < 2)
        {
            return BadRequest(new { message = "El nombre del repartidor es obligatorio." });
        }

        if (string.IsNullOrWhiteSpace(request.Phone) || request.Phone.Trim().Length < 6)
        {
            return BadRequest(new { message = "El teléfono del repartidor es obligatorio." });
        }

        var partner = await _db.Partners.AsNoTracking().FirstOrDefaultAsync(x => x.Id == partnerId);
        if (partner is null)
        {
            return NotFound();
        }

        var entity = new Courier
        {
            Id = Guid.NewGuid(),
            TenantId = partner.TenantId,
            PartnerId = partnerId,
            Name = request.Name.Trim(),
            Phone = request.Phone.Trim(),
            Company = string.IsNullOrWhiteSpace(request.Company) ? null : request.Company.Trim(),
            IsAvailable = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _db.Couriers.Add(entity);
        await _db.SaveChangesAsync();

        return Created(
            $"/v1/partners/{partnerId}/couriers/{entity.Id}",
            new CourierResponse(entity.Id, entity.PartnerId, entity.Name, entity.Phone, entity.Company, entity.IsAvailable));
    }

    [HttpPatch("/v1/partners/{partnerId:guid}/couriers/{courierId:guid}")]
    public async Task<ActionResult<CourierResponse>> Update(Guid partnerId, Guid courierId, CourierUpdateRequest request)
    {
        if (!await CanManagePartnerAsync(partnerId))
        {
            return Forbid();
        }

        var entity = await _db.Couriers.FirstOrDefaultAsync(x => x.Id == courierId && x.PartnerId == partnerId);
        if (entity is null)
        {
            return NotFound();
        }

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            entity.Name = request.Name.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.Phone))
        {
            entity.Phone = request.Phone.Trim();
        }

        if (request.Company is not null)
        {
            entity.Company = string.IsNullOrWhiteSpace(request.Company) ? null : request.Company.Trim();
        }

        if (request.IsAvailable.HasValue)
        {
            entity.IsAvailable = request.IsAvailable.Value;
        }

        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new CourierResponse(entity.Id, entity.PartnerId, entity.Name, entity.Phone, entity.Company, entity.IsAvailable));
    }

    [HttpDelete("/v1/partners/{partnerId:guid}/couriers/{courierId:guid}")]
    public async Task<IActionResult> Delete(Guid partnerId, Guid courierId)
    {
        if (!await CanManagePartnerAsync(partnerId))
        {
            return Forbid();
        }

        var entity = await _db.Couriers.FirstOrDefaultAsync(x => x.Id == courierId && x.PartnerId == partnerId);
        if (entity is null)
        {
            return NotFound();
        }

        var hasActiveDelivery = await _db.Orders.AsNoTracking()
            .AnyAsync(x => x.CourierId == courierId
                && (x.DeliveryStatus == DeliveryStatuses.Assigned
                    || x.DeliveryStatus == DeliveryStatuses.PickedUp
                    || x.DeliveryStatus == DeliveryStatuses.InTransit));
        if (hasActiveDelivery)
        {
            return BadRequest(new { message = "No se puede eliminar: el repartidor tiene envíos activos." });
        }

        _db.Couriers.Remove(entity);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Asigna (o reasigna) un repartidor al pedido y devuelve el link seguro para compartir.</summary>
    [HttpPost("/v1/orders/{orderId:guid}/assign-courier")]
    public async Task<ActionResult<AssignCourierResponse>> AssignCourier(Guid orderId, AssignCourierRequest request)
    {
        var order = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(x => x.Id == orderId);
        if (order is null)
        {
            return NotFound();
        }

        if (!await CanManagePartnerAsync(order.PartnerId))
        {
            return Forbid();
        }

        var (result, error) = await _deliveryService.AssignCourierAsync(orderId, request.CourierId, HttpContext.RequestAborted);
        return result is null ? BadRequest(new { message = error }) : Ok(result);
    }

    /// <summary>Cancela el envío (no la orden): difunde el cambio y revoca el token del repartidor.</summary>
    [HttpPost("/v1/orders/{orderId:guid}/cancel-delivery")]
    public async Task<IActionResult> CancelDelivery(Guid orderId)
    {
        var order = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(x => x.Id == orderId);
        if (order is null)
        {
            return NotFound();
        }

        if (!await CanManagePartnerAsync(order.PartnerId))
        {
            return Forbid();
        }

        var result = await _deliveryService.UpdateStatusAsync(orderId, DeliveryStatuses.Canceled, HttpContext.RequestAborted);
        return result.Ok ? Ok(new { ok = true }) : BadRequest(new { message = result.Message });
    }

    private async Task<bool> CanManagePartnerAsync(Guid partnerId)
    {
        var access = await PartnerAccessAuthorization.EnsurePartnerAccessAsync(
            _db,
            User,
            partnerId,
            _tenantContext.TenantId,
            _tenantContext.PartnerId,
            HttpContext.RequestAborted);

        return access == PartnerAccessResult.Allowed;
    }
}
