using ComunaClick.Acl.Contracts.Users;
using ComunaClick.Acl.Domain;
using ComunaClick.Acl.Persistence;
using ComunaClick.Acl.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ComunaClick.Acl.Controllers;

[ApiController]
[Route("v1/users")]
public sealed class UsersController : ControllerBase
{
    private readonly AclDbContext _db;

    public UsersController(AclDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<User>>> List()
        => Ok(await _db.Users.AsNoTracking().ToListAsync());

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<User>> Get(Guid id)
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return user is null ? NotFound() : Ok(user);
    }

    [HttpPost]
    public async Task<ActionResult<User>> Create(UserCreateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new { message = "Email is required." });
        }

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
        {
            return BadRequest(new { message = "Password must be at least 8 characters." });
        }

        var exists = await _db.Users.AnyAsync(x => x.Email == request.Email);
        if (exists)
        {
            return Conflict(new { message = "Email already exists." });
        }

        var user = new User
        {
            Email = request.Email.Trim(),
            PasswordHash = PasswordHasher.Hash(request.Password),
            DisplayName = request.DisplayName,
            IsActive = request.IsActive,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return Created($"/v1/users/{user.Id}", user);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<User>> Update(Guid id, UserUpdateRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == id);
        if (user is null)
        {
            return NotFound();
        }

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            var exists = await _db.Users.AnyAsync(x => x.Email == request.Email && x.Id != id);
            if (exists)
            {
                return Conflict(new { message = "Email already exists." });
            }
            user.Email = request.Email.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            if (request.Password.Length < 8)
            {
                return BadRequest(new { message = "Password must be at least 8 characters." });
            }
            user.PasswordHash = PasswordHasher.Hash(request.Password);
        }

        if (request.DisplayName is not null)
        {
            user.DisplayName = request.DisplayName;
        }

        if (request.IsActive.HasValue)
        {
            user.IsActive = request.IsActive.Value;
        }

        user.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(user);
    }

    [HttpPost("{id:guid}/reset-password")]
    public async Task<ActionResult<User>> ResetPassword(Guid id, PasswordResetRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
        {
            return BadRequest(new { message = "Password must be at least 8 characters." });
        }

        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == id);
        if (user is null)
        {
            return NotFound();
        }

        user.PasswordHash = PasswordHasher.Hash(request.NewPassword);
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(user);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == id);
        if (user is null)
        {
            return NotFound();
        }

        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("{id:guid}/roles")]
    public async Task<ActionResult<IEnumerable<Role>>> ListRoles(Guid id)
    {
        var roles = await _db.UserRoles
            .AsNoTracking()
            .Where(x => x.UserId == id)
            .Select(x => x.Role)
            .ToListAsync();
        return Ok(roles);
    }

    [HttpPost("{id:guid}/roles")]
    public async Task<IActionResult> AddRole(Guid id, UserRoleRequest request)
    {
        var exists = await _db.UserRoles.AnyAsync(x => x.UserId == id && x.RoleId == request.RoleId);
        if (exists)
        {
            return Conflict(new { message = "User already has this role." });
        }

        _db.UserRoles.Add(new UserRole { UserId = id, RoleId = request.RoleId });
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:guid}/roles/{roleId:guid}")]
    public async Task<IActionResult> RemoveRole(Guid id, Guid roleId)
    {
        var relation = await _db.UserRoles.FirstOrDefaultAsync(x => x.UserId == id && x.RoleId == roleId);
        if (relation is null)
        {
            return NotFound();
        }

        _db.UserRoles.Remove(relation);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("{id:guid}/scopes")]
    public async Task<ActionResult<IEnumerable<UserTenantScope>>> ListScopes(Guid id)
    {
        var scopes = await _db.UserTenantScopes.AsNoTracking()
            .Where(x => x.UserId == id)
            .ToListAsync();
        return Ok(scopes);
    }

    [HttpPost("{id:guid}/scopes")]
    public async Task<ActionResult<UserTenantScope>> CreateScope(Guid id, UserTenantScopeCreateRequest request)
    {
        var scopeType = string.IsNullOrWhiteSpace(request.ScopeType) ? "tenant" : request.ScopeType.Trim();
        var exists = await _db.UserTenantScopes.AnyAsync(x =>
            x.UserId == id &&
            x.TenantId == request.TenantId &&
            x.PartnerId == request.PartnerId &&
            x.ScopeType == scopeType);

        if (exists)
        {
            return Conflict(new { message = "Scope already exists." });
        }

        var scope = new UserTenantScope
        {
            UserId = id,
            TenantId = request.TenantId,
            PartnerId = request.PartnerId,
            ScopeType = scopeType,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.UserTenantScopes.Add(scope);
        await _db.SaveChangesAsync();
        return Created($"/v1/users/{id}/scopes/{scope.Id}", scope);
    }

    [HttpDelete("{id:guid}/scopes/{scopeId:guid}")]
    public async Task<IActionResult> DeleteScope(Guid id, Guid scopeId)
    {
        var scope = await _db.UserTenantScopes.FirstOrDefaultAsync(x => x.UserId == id && x.Id == scopeId);
        if (scope is null)
        {
            return NotFound();
        }

        _db.UserTenantScopes.Remove(scope);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
