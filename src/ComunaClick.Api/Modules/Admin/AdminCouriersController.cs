using ComunaClick.Api.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ComunaClick.Api.Modules.Admin;

[ApiController]
[Route("v1/admin/couriers")]
[Authorize(Policy = "platform.admin")]
public sealed class AdminCouriersController : ControllerBase
{
    private readonly CoreDbContext _db;

    public AdminCouriersController(CoreDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminCourierListItemDto>>> List(
        [FromQuery] Guid? partnerId,
        CancellationToken cancellationToken)
    {
        var query = _db.Couriers.IgnoreQueryFilters().AsNoTracking().AsQueryable();
        if (partnerId.HasValue)
        {
            query = query.Where(x => x.PartnerId == partnerId.Value);
        }

        var couriers = await query
            .OrderBy(x => x.Name)
            .Take(500)
            .Select(x => new
            {
                x.Id,
                x.TenantId,
                x.PartnerId,
                x.Name,
                x.Phone,
                x.Company,
                x.Email,
                x.UserId,
                x.Kind,
                x.IsAvailable,
                x.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        if (couriers.Count == 0)
        {
            return Ok(Array.Empty<AdminCourierListItemDto>());
        }

        var courierIds = couriers.Select(x => x.Id).ToList();
        var partnerIds = couriers.Select(x => x.PartnerId).Distinct().ToList();

        var partnerNames = await _db.Partners
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => partnerIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Name })
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);

        // El payee del courier es una fila Seller con Id = Courier.Id (CourierPayeeService).
        var mpStatuses = await _db.SellerMercadoPagoAccounts
            .AsNoTracking()
            .Where(x => courierIds.Contains(x.SellerId))
            .Select(x => new { x.SellerId, x.ConnectionStatus })
            .ToDictionaryAsync(x => x.SellerId, x => x.ConnectionStatus, cancellationToken);

        var pendings = await _db.DeliverySettlements
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.CourierId != null
                && courierIds.Contains(x.CourierId.Value)
                && (x.Status == "pending" || x.Status == "manual" || x.Status == "processing"))
            .GroupBy(x => x.CourierId!.Value)
            .Select(g => new { CourierId = g.Key, Count = g.Count(), Amount = g.Sum(x => x.NetToCourierAmount) })
            .ToDictionaryAsync(x => x.CourierId, cancellationToken);

        var items = couriers
            .Select(x => new AdminCourierListItemDto(
                x.Id,
                x.TenantId,
                x.PartnerId,
                partnerNames.TryGetValue(x.PartnerId, out var partnerName) ? partnerName : "(sin partner)",
                x.Name,
                x.Phone,
                x.Company,
                x.Email,
                x.Kind,
                x.IsAvailable,
                x.UserId.HasValue,
                mpStatuses.TryGetValue(x.Id, out var mpStatus) ? mpStatus : "disconnected",
                pendings.TryGetValue(x.Id, out var pending) ? pending.Count : 0,
                pendings.TryGetValue(x.Id, out var pendingAmount) ? pendingAmount.Amount : 0m,
                x.UpdatedAt))
            .ToList();

        return Ok(items);
    }
}
