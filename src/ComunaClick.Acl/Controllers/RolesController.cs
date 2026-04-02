using ComunaClick.Acl.Contracts.Roles;
using ComunaClick.Acl.Domain;
using ComunaClick.Acl.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ComunaClick.Acl.Controllers;

[ApiController]
[Route("v1/roles")]
[Authorize(Policy = "platform.admin")]
public sealed class RolesController : ControllerBase
{
    private readonly AclDbContext _db;

    public RolesController(AclDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Role>>> List()
        => Ok(await _db.Roles.AsNoTracking().ToListAsync());

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Role>> Get(Guid id)
    {
        var role = await _db.Roles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return role is null ? NotFound() : Ok(role);
    }

    [HttpPost]
    public async Task<ActionResult<Role>> Create(RoleCreateRequest request)
    {
        var exists = await _db.Roles.AnyAsync(x => x.Name == request.Name);
        if (exists)
        {
            return Conflict(new { message = "Role name already exists." });
        }

        var role = new Role
        {
            Name = request.Name.Trim(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.Roles.Add(role);
        await _db.SaveChangesAsync();
        return Created($"/v1/roles/{role.Id}", role);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<Role>> Update(Guid id, RoleUpdateRequest request)
    {
        var role = await _db.Roles.FirstOrDefaultAsync(x => x.Id == id);
        if (role is null)
        {
            return NotFound();
        }

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            var exists = await _db.Roles.AnyAsync(x => x.Name == request.Name && x.Id != id);
            if (exists)
            {
                return Conflict(new { message = "Role name already exists." });
            }
            role.Name = request.Name.Trim();
        }

        await _db.SaveChangesAsync();
        return Ok(role);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var role = await _db.Roles.FirstOrDefaultAsync(x => x.Id == id);
        if (role is null)
        {
            return NotFound();
        }

        _db.Roles.Remove(role);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("{id:guid}/permissions")]
    public async Task<ActionResult<IEnumerable<Permission>>> ListPermissions(Guid id)
    {
        var permissions = await _db.RolePermissions
            .AsNoTracking()
            .Where(x => x.RoleId == id)
            .Select(x => x.Permission)
            .ToListAsync();
        return Ok(permissions);
    }

    [HttpPost("{id:guid}/permissions")]
    public async Task<IActionResult> AddPermission(Guid id, RolePermissionRequest request)
    {
        var exists = await _db.RolePermissions.AnyAsync(x => x.RoleId == id && x.PermissionId == request.PermissionId);
        if (exists)
        {
            return Conflict(new { message = "Role already has this permission." });
        }

        _db.RolePermissions.Add(new RolePermission { RoleId = id, PermissionId = request.PermissionId });
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:guid}/permissions/{permissionId:guid}")]
    public async Task<IActionResult> RemovePermission(Guid id, Guid permissionId)
    {
        var relation = await _db.RolePermissions.FirstOrDefaultAsync(x => x.RoleId == id && x.PermissionId == permissionId);
        if (relation is null)
        {
            return NotFound();
        }

        _db.RolePermissions.Remove(relation);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
