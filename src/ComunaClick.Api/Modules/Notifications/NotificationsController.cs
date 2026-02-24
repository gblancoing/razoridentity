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

    public NotificationsController(CoreDbContext db)
    {
        _db = db;
    }

    [HttpGet("/v1/partners/{partnerId:guid}/notifications")]
    public async Task<ActionResult<IEnumerable<Interaction>>> ListByPartner(Guid partnerId)
    {
        var notifications = await _db.Interactions.AsNoTracking()
            .Where(x => x.PartnerId == partnerId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        return Ok(notifications);
    }
}
