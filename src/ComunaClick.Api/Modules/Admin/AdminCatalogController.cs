using ComunaClick.Api.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ComunaClick.Api.Modules.Admin;

[ApiController]
[Route("v1/admin/categories")]
[Authorize(Policy = "platform.admin")]
public sealed class AdminCatalogController : ControllerBase
{
    private readonly CoreDbContext _db;

    public AdminCatalogController(CoreDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<AdminCategoryListItemDto>>> List(CancellationToken cancellationToken)
    {
        var items = await _db.ProductCategories
            .IgnoreQueryFilters()
            .AsNoTracking()
            .GroupJoin(
                _db.ProductSubcategories.IgnoreQueryFilters(),
                category => category.Id,
                subcategory => subcategory.CategoryId,
                (category, subs) => new AdminCategoryListItemDto(
                    category.Id,
                    category.Code,
                    category.Name,
                    category.SortOrder,
                    category.IsActive,
                    subs.Count()))
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<AdminCategoryListItemDto>> Update(Guid id, AdminCategoryUpdateRequest request, CancellationToken cancellationToken)
    {
        var entity = await _db.ProductCategories
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
        {
            return NotFound();
        }

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            entity.Name = request.Name.Trim();
        }

        if (request.SortOrder.HasValue)
        {
            entity.SortOrder = request.SortOrder.Value;
        }

        if (request.IsActive.HasValue)
        {
            entity.IsActive = request.IsActive.Value;
        }

        await _db.SaveChangesAsync(cancellationToken);

        var subcategoryCount = await _db.ProductSubcategories
            .IgnoreQueryFilters()
            .CountAsync(x => x.CategoryId == entity.Id, cancellationToken);

        return Ok(new AdminCategoryListItemDto(
            entity.Id,
            entity.Code,
            entity.Name,
            entity.SortOrder,
            entity.IsActive,
            subcategoryCount));
    }
}
