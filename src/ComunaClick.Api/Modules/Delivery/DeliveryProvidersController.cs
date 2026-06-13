using ComunaClick.Api.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ComunaClick.Api.Modules.Delivery;

[ApiController]
[Authorize(Policy = "buyer.customer")]
[Route("v1/delivery/providers")]
public sealed class DeliveryProvidersController : ControllerBase
{
    private readonly CoreDbContext _db;
    private readonly ITenantContext _tenantContext;

    public DeliveryProvidersController(CoreDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<object>>> ListByPartner([FromQuery] Guid partnerId, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;
        if (!tenantId.HasValue)
        {
            return BadRequest(new { message = "TenantId is required." });
        }

        if (partnerId == Guid.Empty)
        {
            return BadRequest(new { message = "partnerId is required." });
        }

        var partner = await _db.Partners.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == partnerId && x.TenantId == tenantId.Value && x.IsVisible, cancellationToken);

        if (partner is null)
        {
            return NotFound(new { message = "Partner not found." });
        }

        // El transportista preferido del comercio lista primero; luego por
        // especificidad de zona (comuna > región > global).
        var providers = await _db.DeliveryProviders.AsNoTracking()
            .WhereServesPartner(partner)
            .OrderByDescending(x => x.Id == partner.PreferredDeliveryProviderId)
            .ThenByDescending(x => x.ComunaId.HasValue)
            .ThenByDescending(x => x.RegionId.HasValue)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return Ok(providers.Select(x => new
        {
            x.Id,
            x.Name,
            x.ContactName,
            x.ContactPhone,
            x.ContactEmail,
            x.BaseFee,
            x.EstimatedMinutes,
            x.RegionId,
            x.ComunaId
        }));
    }
}
