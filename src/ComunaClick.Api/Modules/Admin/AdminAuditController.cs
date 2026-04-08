using ComunaClick.Api.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ComunaClick.Api.Modules.Admin;

[ApiController]
[Route("v1/admin/audit")]
[Authorize(Policy = "platform.admin")]
public sealed class AdminAuditController : ControllerBase
{
    private readonly CoreDbContext _db;

    public AdminAuditController(CoreDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<AdminAuditEventDto>>> List(CancellationToken cancellationToken)
    {
        var partnerEvents = await _db.Partners.IgnoreQueryFilters().AsNoTracking()
            .OrderByDescending(x => x.UpdatedAt)
            .Take(10)
            .Select(x => new AdminAuditEventDto("partner", x.Id.ToString(), "updated", $"Partner {x.Name} actualizado", x.UpdatedAt))
            .ToListAsync(cancellationToken);

        var tenantEvents = await _db.Tenants.IgnoreQueryFilters().AsNoTracking()
            .OrderByDescending(x => x.UpdatedAt)
            .Take(10)
            .Select(x => new AdminAuditEventDto("tenant", x.Id.ToString(), "updated", $"Tenant {x.Name} actualizado", x.UpdatedAt))
            .ToListAsync(cancellationToken);

        var orderEvents = await _db.Orders.IgnoreQueryFilters().AsNoTracking()
            .OrderByDescending(x => x.UpdatedAt)
            .Take(10)
            .Select(x => new AdminAuditEventDto("order", x.Id.ToString(), x.Status, $"Orden {x.Id} con estado {x.Status}", x.UpdatedAt))
            .ToListAsync(cancellationToken);

        var items = partnerEvents
            .Concat(tenantEvents)
            .Concat(orderEvents)
            .OrderByDescending(x => x.OccurredAt)
            .Take(25)
            .ToList();

        return Ok(items);
    }
}
