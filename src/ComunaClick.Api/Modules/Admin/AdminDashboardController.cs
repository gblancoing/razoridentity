using ComunaClick.Api.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ComunaClick.Api.Modules.Admin;

[ApiController]
[Route("v1/admin/dashboard")]
[Authorize(Policy = "platform.admin")]
public sealed class AdminDashboardController : ControllerBase
{
    private readonly CoreDbContext _db;

    public AdminDashboardController(CoreDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<AdminDashboardDto>> Get(CancellationToken cancellationToken)
    {
        var tenants = _db.Tenants.IgnoreQueryFilters();
        var partners = _db.Partners.IgnoreQueryFilters();
        var categories = _db.ProductCategories.IgnoreQueryFilters();
        var orders = _db.Orders.IgnoreQueryFilters();
        var bookings = _db.Bookings.IgnoreQueryFilters();
        var leads = _db.Leads.IgnoreQueryFilters();

        var dto = new AdminDashboardDto(
            await tenants.CountAsync(cancellationToken),
            await partners.CountAsync(cancellationToken),
            await partners.CountAsync(x => x.IsVisible, cancellationToken),
            await categories.CountAsync(cancellationToken),
            await orders.CountAsync(cancellationToken),
            await bookings.CountAsync(cancellationToken),
            await leads.CountAsync(cancellationToken),
            await partners.AsNoTracking()
                .OrderByDescending(x => x.UpdatedAt)
                .Take(6)
                .Select(x => new AdminRecentPartnerDto(x.Id, x.Name, x.Type, x.IsVisible, x.UpdatedAt))
                .ToListAsync(cancellationToken),
            await orders.AsNoTracking()
                .OrderByDescending(x => x.CreatedAt)
                .Take(6)
                .Select(x => new AdminRecentOrderDto(x.Id, x.PartnerId, x.Status, x.TotalAmount, x.Currency, x.CreatedAt))
                .ToListAsync(cancellationToken));

        return Ok(dto);
    }
}
