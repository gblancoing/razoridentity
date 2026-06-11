using ComunaClick.Api.Modules.Catalog.Contracts;
using ComunaClick.Api.Persistence;
using ComunaClick.Api.Persistence.Entities;
using ComunaClick.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ComunaClick.Api.Modules.Catalog;

[ApiController]
[Authorize(Policy = "partner.staff")]
[Route("v1/partners/{partnerId:guid}/catalog-categories")]
public sealed class PartnerCatalogCategoriesController : ControllerBase
{
    private readonly CoreDbContext _db;
    private readonly ITenantContext _tenantContext;

    public PartnerCatalogCategoriesController(CoreDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PartnerCatalogCategoryResponse>>> List(Guid partnerId)
    {
        if (!await CanManagePartnerAsync(partnerId))
        {
            return Forbid();
        }

        var items = await _db.PartnerCatalogCategories.AsNoTracking()
            .Where(x => x.PartnerId == partnerId)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Select(x => new PartnerCatalogCategoryResponse(x.Id, x.PartnerId, x.Name, x.ParentId, x.SortOrder, x.IsActive))
            .ToListAsync();

        return Ok(items);
    }

    [HttpPost]
    public async Task<ActionResult<PartnerCatalogCategoryResponse>> Create(
        Guid partnerId,
        PartnerCatalogCategoryCreateRequest request)
    {
        if (!await CanManagePartnerAsync(partnerId))
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { message = "El nombre de la sección es obligatorio." });
        }

        var partner = await _db.Partners.AsNoTracking().FirstOrDefaultAsync(x => x.Id == partnerId);
        if (partner is null)
        {
            return NotFound();
        }

        if (request.ParentId is { } parentId)
        {
            var parentCheck = await PartnerCatalogCategoryRules.ValidateParentAsync(
                _db, partnerId, parentId, cancellationToken: HttpContext.RequestAborted);
            if (!parentCheck.Ok)
            {
                return BadRequest(new { message = parentCheck.Message });
            }
        }

        var sortOrder = request.SortOrder ?? await _db.PartnerCatalogCategories
            .Where(x => x.PartnerId == partnerId)
            .Select(x => (int?)x.SortOrder)
            .MaxAsync() ?? -1;
        sortOrder++;

        var entity = new PartnerCatalogCategory
        {
            Id = Guid.NewGuid(),
            TenantId = partner.TenantId,
            PartnerId = partnerId,
            Name = request.Name.Trim(),
            ParentId = request.ParentId,
            SortOrder = sortOrder,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _db.PartnerCatalogCategories.Add(entity);
        await _db.SaveChangesAsync();

        return Created(
            $"/v1/partners/{partnerId}/catalog-categories/{entity.Id}",
            new PartnerCatalogCategoryResponse(entity.Id, entity.PartnerId, entity.Name, entity.ParentId, entity.SortOrder, entity.IsActive));
    }

    [HttpPatch("{categoryId:guid}")]
    public async Task<ActionResult<PartnerCatalogCategoryResponse>> Update(
        Guid partnerId,
        Guid categoryId,
        PartnerCatalogCategoryUpdateRequest request)
    {
        if (!await CanManagePartnerAsync(partnerId))
        {
            return Forbid();
        }

        var entity = await _db.PartnerCatalogCategories.FirstOrDefaultAsync(x => x.Id == categoryId && x.PartnerId == partnerId);
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

        if (request.ClearParent == true)
        {
            entity.ParentId = null;
        }
        else if (request.ParentId is { } parentId && parentId != entity.ParentId)
        {
            var parentCheck = await PartnerCatalogCategoryRules.ValidateParentAsync(
                _db, partnerId, parentId, entity.Id, HttpContext.RequestAborted);
            if (!parentCheck.Ok)
            {
                return BadRequest(new { message = parentCheck.Message });
            }

            entity.ParentId = parentId;
        }

        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new PartnerCatalogCategoryResponse(entity.Id, entity.PartnerId, entity.Name, entity.ParentId, entity.SortOrder, entity.IsActive));
    }

    [HttpDelete("{categoryId:guid}")]
    public async Task<IActionResult> Delete(Guid partnerId, Guid categoryId)
    {
        if (!await CanManagePartnerAsync(partnerId))
        {
            return Forbid();
        }

        var entity = await _db.PartnerCatalogCategories.FirstOrDefaultAsync(x => x.Id == categoryId && x.PartnerId == partnerId);
        if (entity is null)
        {
            return NotFound();
        }

        var deleteCheck = await PartnerCatalogCategoryRules.CanDeleteAsync(_db, categoryId, HttpContext.RequestAborted);
        if (!deleteCheck.Ok)
        {
            return BadRequest(new { message = deleteCheck.Message });
        }

        _db.PartnerCatalogCategories.Remove(entity);
        await _db.SaveChangesAsync();
        return NoContent();
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
