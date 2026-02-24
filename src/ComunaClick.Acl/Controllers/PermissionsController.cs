using ComunaClick.Acl.Contracts.Permissions;
using ComunaClick.Acl.Domain;
using ComunaClick.Acl.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ComunaClick.Acl.Controllers;

[ApiController]
[Route("v1/permissions")]
public sealed class PermissionsController : ControllerBase
{
    private readonly AclDbContext _db;

    public PermissionsController(AclDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Permission>>> List()
        => Ok(await _db.Permissions.AsNoTracking().ToListAsync());

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Permission>> Get(Guid id)
    {
        var permission = await _db.Permissions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return permission is null ? NotFound() : Ok(permission);
    }

    [HttpPost]
    public async Task<ActionResult<Permission>> Create(PermissionCreateRequest request)
    {
        var exists = await _db.Permissions.AnyAsync(x => x.Code == request.Code);
        if (exists)
        {
            return Conflict(new { message = "Permission code already exists." });
        }

        var permission = new Permission
        {
            Code = request.Code.Trim(),
            Description = request.Description,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.Permissions.Add(permission);
        await _db.SaveChangesAsync();
        return Created($"/v1/permissions/{permission.Id}", permission);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<Permission>> Update(Guid id, PermissionUpdateRequest request)
    {
        var permission = await _db.Permissions.FirstOrDefaultAsync(x => x.Id == id);
        if (permission is null)
        {
            return NotFound();
        }

        if (!string.IsNullOrWhiteSpace(request.Code))
        {
            var exists = await _db.Permissions.AnyAsync(x => x.Code == request.Code && x.Id != id);
            if (exists)
            {
                return Conflict(new { message = "Permission code already exists." });
            }
            permission.Code = request.Code.Trim();
        }

        if (request.Description is not null)
        {
            permission.Description = request.Description;
        }

        await _db.SaveChangesAsync();
        return Ok(permission);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var permission = await _db.Permissions.FirstOrDefaultAsync(x => x.Id == id);
        if (permission is null)
        {
            return NotFound();
        }

        _db.Permissions.Remove(permission);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
