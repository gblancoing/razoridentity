using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ComunaClick.Api.Modules.Crm.Contracts;
using ComunaClick.Api.Persistence;
using ComunaClick.Api.Persistence.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ComunaClick.Api.Modules.Crm;

[ApiController]
[Authorize(Policy = "buyer.profile")]
[Route("v1/buyer/customer")]
public sealed class BuyerCustomerController : ControllerBase
{
    private readonly CoreDbContext _db;
    private readonly ITenantContext _tenantContext;

    public BuyerCustomerController(CoreDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    [HttpPost("ensure")]
    public async Task<ActionResult<Customer>> Ensure([FromBody] BuyerCustomerEnsureRequest? request)
    {
        var tenantId = request?.TenantId ?? _tenantContext.TenantId;
        if (!tenantId.HasValue || tenantId.Value == Guid.Empty)
        {
            return BadRequest(new { message = "TenantId is required." });
        }

        var email = ResolveEmail(User);
        if (string.IsNullOrWhiteSpace(email))
        {
            return BadRequest(new { message = "Authenticated email claim is required." });
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var fullName = ResolveName(User);

        var customer = await _db.Customers
            .FirstOrDefaultAsync(x => x.TenantId == tenantId.Value && x.Email != null && x.Email.ToLower() == normalizedEmail);

        if (customer is null)
        {
            customer = new Customer
            {
                TenantId = tenantId.Value,
                Email = normalizedEmail,
                FullName = fullName,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            _db.Customers.Add(customer);
            await _db.SaveChangesAsync();
            return Ok(customer);
        }

        var changed = false;
        if (string.IsNullOrWhiteSpace(customer.FullName) && !string.IsNullOrWhiteSpace(fullName))
        {
            customer.FullName = fullName;
            changed = true;
        }

        if (changed)
        {
            customer.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync();
        }

        return Ok(customer);
    }

    [HttpGet]
    public async Task<ActionResult<Customer>> GetCurrent([FromQuery] Guid? tenantId)
    {
        var tid = tenantId ?? _tenantContext.TenantId;
        if (!tid.HasValue || tid.Value == Guid.Empty)
        {
            return BadRequest(new { message = "TenantId is required." });
        }

        var email = ResolveEmail(User);
        if (string.IsNullOrWhiteSpace(email))
        {
            return BadRequest(new { message = "Authenticated email claim is required." });
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var customer = await _db.Customers.AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tid.Value && x.Email != null && x.Email.ToLower() == normalizedEmail);

        return customer is null ? NotFound() : Ok(customer);
    }

    [HttpPatch]
    public async Task<ActionResult<Customer>> UpdateProfile([FromBody] CustomerUpdateRequest request, [FromQuery] Guid? tenantId)
    {
        var tid = tenantId ?? _tenantContext.TenantId;
        if (!tid.HasValue || tid.Value == Guid.Empty)
        {
            return BadRequest(new { message = "TenantId is required." });
        }

        var email = ResolveEmail(User);
        if (string.IsNullOrWhiteSpace(email))
        {
            return BadRequest(new { message = "Authenticated email claim is required." });
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var customer = await _db.Customers.FirstOrDefaultAsync(x =>
            x.TenantId == tid.Value && x.Email != null && x.Email.ToLower() == normalizedEmail);

        if (customer is null)
        {
            return NotFound();
        }

        if (request.Email is not null)
        {
            customer.Email = request.Email.Trim().ToLowerInvariant();
        }

        if (request.Phone is not null)
        {
            customer.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
        }

        if (request.FullName is not null)
        {
            customer.FullName = string.IsNullOrWhiteSpace(request.FullName) ? null : request.FullName.Trim();
        }

        if (request.AvatarUrl is not null)
        {
            customer.AvatarUrl = CustomerProfileHelper.SanitizeAvatarUrl(request.AvatarUrl);
        }

        customer.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(customer);
    }

    private static string? ResolveEmail(ClaimsPrincipal user)
    {
        return user.FindFirst(ClaimTypes.Email)?.Value
            ?? user.FindFirst(JwtRegisteredClaimNames.Email)?.Value
            ?? user.FindFirst("email")?.Value;
    }

    private static string? ResolveName(ClaimsPrincipal user)
    {
        return user.FindFirst(ClaimTypes.Name)?.Value
            ?? user.FindFirst("name")?.Value
            ?? user.FindFirst(JwtRegisteredClaimNames.Name)?.Value;
    }
}
