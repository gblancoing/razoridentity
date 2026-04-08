using ComunaClick.Api.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ComunaClick.Api.Modules.Admin;

[ApiController]
[Route("v1/admin/partners")]
[Authorize(Policy = "platform.admin")]
public sealed class AdminPartnersController : ControllerBase
{
    private readonly CoreDbContext _db;

    public AdminPartnersController(CoreDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<AdminPartnerListItemDto>>> List(CancellationToken cancellationToken)
    {
        var items = await _db.Partners
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(x => x.Subcategory)
            .ThenInclude(x => x!.Category)
            .OrderByDescending(x => x.UpdatedAt)
            .Select(x => new AdminPartnerListItemDto(
                x.Id,
                x.TenantId,
                x.Type,
                x.Name,
                x.Subcategory != null ? x.Subcategory.Category.Name : null,
                x.Subcategory != null ? x.Subcategory.Name : null,
                x.Address,
                x.Phone,
                x.Email,
                x.IsVisible,
                x.UpdatedAt))
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<AdminPartnerListItemDto>> Update(Guid id, AdminPartnerUpdateRequest request, CancellationToken cancellationToken)
    {
        var entity = await _db.Partners
            .IgnoreQueryFilters()
            .Include(x => x.Subcategory)
            .ThenInclude(x => x!.Category)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
        {
            return NotFound();
        }

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            entity.Name = request.Name.Trim();
        }

        if (request.Address is not null)
        {
            entity.Address = Clean(request.Address);
        }

        if (request.Phone is not null)
        {
            entity.Phone = Clean(request.Phone);
        }

        if (request.Email is not null)
        {
            entity.Email = Clean(request.Email);
        }

        if (request.IsVisible.HasValue)
        {
            entity.IsVisible = request.IsVisible.Value;
        }

        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new AdminPartnerListItemDto(
            entity.Id,
            entity.TenantId,
            entity.Type,
            entity.Name,
            entity.Subcategory?.Category?.Name,
            entity.Subcategory?.Name,
            entity.Address,
            entity.Phone,
            entity.Email,
            entity.IsVisible,
            entity.UpdatedAt));
    }

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
