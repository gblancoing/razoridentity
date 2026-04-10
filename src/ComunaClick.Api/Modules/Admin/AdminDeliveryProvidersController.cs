using ComunaClick.Api.Persistence;
using ComunaClick.Api.Persistence.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ComunaClick.Api.Modules.Admin;

[ApiController]
[Route("v1/admin/delivery-providers")]
[Authorize(Policy = "platform.admin")]
public sealed class AdminDeliveryProvidersController : ControllerBase
{
    private readonly CoreDbContext _db;

    public AdminDeliveryProvidersController(CoreDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<AdminDeliveryProviderDto>>> List(CancellationToken cancellationToken)
    {
        var items = await _db.DeliveryProviders
            .IgnoreQueryFilters()
            .AsNoTracking()
            .OrderByDescending(x => x.UpdatedAt)
            .Select(x => new AdminDeliveryProviderDto(
                x.Id,
                x.TenantId,
                x.RegionId,
                x.ComunaId,
                x.Name,
                x.ContactName,
                x.ContactPhone,
                x.ContactEmail,
                x.BaseFee,
                x.EstimatedMinutes,
                x.IsActive,
                x.UpdatedAt))
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpPost]
    public async Task<ActionResult<AdminDeliveryProviderDto>> Create(AdminDeliveryProviderUpsertRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { message = "Name is required." });
        }

        var entity = new DeliveryProvider
        {
            TenantId = request.TenantId,
            RegionId = request.RegionId,
            ComunaId = request.ComunaId,
            Name = request.Name.Trim(),
            ContactName = Clean(request.ContactName),
            ContactPhone = Clean(request.ContactPhone),
            ContactEmail = Clean(request.ContactEmail),
            BaseFee = Math.Max(0, request.BaseFee ?? 0),
            EstimatedMinutes = request.EstimatedMinutes,
            IsActive = request.IsActive ?? true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _db.DeliveryProviders.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        return Created($"/v1/admin/delivery-providers/{entity.Id}", ToDto(entity));
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<AdminDeliveryProviderDto>> Update(Guid id, AdminDeliveryProviderUpsertRequest request, CancellationToken cancellationToken)
    {
        var entity = await _db.DeliveryProviders
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

        if (request.ContactName is not null)
        {
            entity.ContactName = Clean(request.ContactName);
        }

        if (request.ContactPhone is not null)
        {
            entity.ContactPhone = Clean(request.ContactPhone);
        }

        if (request.ContactEmail is not null)
        {
            entity.ContactEmail = Clean(request.ContactEmail);
        }

        if (request.BaseFee.HasValue)
        {
            entity.BaseFee = Math.Max(0, request.BaseFee.Value);
        }

        if (request.EstimatedMinutes.HasValue)
        {
            entity.EstimatedMinutes = request.EstimatedMinutes.Value;
        }

        if (request.IsActive.HasValue)
        {
            entity.IsActive = request.IsActive.Value;
        }

        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(ToDto(entity));
    }

    private static AdminDeliveryProviderDto ToDto(DeliveryProvider entity)
        => new(
            entity.Id,
            entity.TenantId,
            entity.RegionId,
            entity.ComunaId,
            entity.Name,
            entity.ContactName,
            entity.ContactPhone,
            entity.ContactEmail,
            entity.BaseFee,
            entity.EstimatedMinutes,
            entity.IsActive,
            entity.UpdatedAt);

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
