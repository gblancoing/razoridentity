using ComunaClick.Api.Persistence;
using ComunaClick.Api.Persistence.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ComunaClick.Api.Modules.Notifications;

[ApiController]
[Authorize(Policy = "partner.staff")]
[Route("v1/notifications")]
public sealed class NotificationsController : ControllerBase
{
    private readonly CoreDbContext _db;
    private readonly ITenantContext _tenantContext;

    public NotificationsController(CoreDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    [HttpGet("/v1/partners/{partnerId:guid}/notifications")]
    public async Task<ActionResult<IEnumerable<Interaction>>> ListByPartner(Guid partnerId, [FromQuery] bool includeArchived = false)
    {
        var tenantId = _tenantContext.TenantId;
        if (!tenantId.HasValue)
        {
            return BadRequest(new { message = "TenantId is required." });
        }

        var scopedPartner = _tenantContext.PartnerId;
        if (scopedPartner.HasValue && scopedPartner.Value != partnerId)
        {
            return Forbid();
        }

        var notificationsQuery = _db.Interactions.AsNoTracking()
            .Where(x => x.TenantId == tenantId.Value && x.PartnerId == partnerId);

        if (!includeArchived)
        {
            notificationsQuery = notificationsQuery.Where(x => !x.ArchivedAt.HasValue);
        }

        var notifications = await notificationsQuery
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        return Ok(notifications);
    }

    [HttpPatch("{id:guid}/read")]
    public async Task<ActionResult<Interaction>> MarkRead(Guid id)
    {
        var tenantId = _tenantContext.TenantId;
        if (!tenantId.HasValue)
        {
            return BadRequest(new { message = "TenantId is required." });
        }

        var notification = await _db.Interactions.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId.Value);
        if (notification is null)
        {
            return NotFound();
        }

        if (_tenantContext.PartnerId.HasValue && notification.PartnerId.HasValue && _tenantContext.PartnerId.Value != notification.PartnerId.Value)
        {
            return Forbid();
        }

        notification.ReadAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(notification);
    }

    [HttpPatch("{id:guid}/archive")]
    public async Task<ActionResult<Interaction>> Archive(Guid id)
    {
        var tenantId = _tenantContext.TenantId;
        if (!tenantId.HasValue)
        {
            return BadRequest(new { message = "TenantId is required." });
        }

        var notification = await _db.Interactions.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId.Value);
        if (notification is null)
        {
            return NotFound();
        }

        if (_tenantContext.PartnerId.HasValue && notification.PartnerId.HasValue && _tenantContext.PartnerId.Value != notification.PartnerId.Value)
        {
            return Forbid();
        }

        notification.ArchivedAt = DateTimeOffset.UtcNow;
        notification.ReadAt ??= DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(notification);
    }
}
