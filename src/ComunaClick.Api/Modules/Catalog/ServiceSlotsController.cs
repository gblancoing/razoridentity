using ComunaClick.Api.Modules.Catalog.Contracts;
using ComunaClick.Api.Persistence;
using ComunaClick.Api.Persistence.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ComunaClick.Api.Modules.Catalog;

[ApiController]
[Authorize(Policy = "partner.staff")]
public sealed class ServiceSlotsController : ControllerBase
{
    private readonly CoreDbContext _db;
    private readonly ITenantContext _tenantContext;

    public ServiceSlotsController(CoreDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    [HttpGet("/v1/services/{serviceId:guid}/slots")]
    public async Task<ActionResult<IEnumerable<ServiceSlot>>> List(Guid serviceId)
    {
        var slots = await _db.ServiceSlots.AsNoTracking()
            .Where(x => x.ServiceId == serviceId)
            .OrderBy(x => x.StartAt)
            .ToListAsync();
        return Ok(slots);
    }

    [HttpPost("/v1/services/{serviceId:guid}/slots")]
    public async Task<ActionResult<ServiceSlot>> Create(Guid serviceId, ServiceSlotCreateRequest request)
    {
        var tenantId = _tenantContext.TenantId;
        if (!tenantId.HasValue)
        {
            return BadRequest(new { message = "TenantId is required." });
        }

        var partnerId = _tenantContext.PartnerId ?? request.PartnerId;
        if (partnerId == Guid.Empty)
        {
            return BadRequest(new { message = "PartnerId is required." });
        }

        if (_tenantContext.PartnerId.HasValue && request.PartnerId != Guid.Empty && request.PartnerId != _tenantContext.PartnerId.Value)
        {
            return Forbid();
        }

        if (request.EndAt <= request.StartAt)
        {
            return BadRequest(new { message = "EndAt must be after StartAt." });
        }

        var slot = new ServiceSlot
        {
            TenantId = tenantId.Value,
            PartnerId = partnerId,
            ServiceId = serviceId,
            StartAt = request.StartAt,
            EndAt = request.EndAt,
            Capacity = request.Capacity ?? 1,
            IsAvailable = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.ServiceSlots.Add(slot);
        await _db.SaveChangesAsync();
        return Created($"/v1/service-slots/{slot.Id}", slot);
    }

    [HttpPatch("/v1/service-slots/{id:guid}")]
    public async Task<ActionResult<ServiceSlot>> Update(Guid id, ServiceSlotUpdateRequest request)
    {
        var slot = await _db.ServiceSlots.FirstOrDefaultAsync(x => x.Id == id);
        if (slot is null)
        {
            return NotFound();
        }

        if (request.IsAvailable.HasValue)
        {
            slot.IsAvailable = request.IsAvailable.Value;
        }

        if (request.Capacity.HasValue)
        {
            slot.Capacity = request.Capacity.Value;
        }

        await _db.SaveChangesAsync();
        return Ok(slot);
    }
}
