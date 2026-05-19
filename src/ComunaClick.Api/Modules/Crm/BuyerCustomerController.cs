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
[Authorize(Policy = "buyer.customer")]
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
        var tenantId = request?.TenantId
            ?? _tenantContext.TenantId;
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
            .IgnoreQueryFilters()
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
            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                _db.Entry(customer).State = EntityState.Detached;
                customer = await _db.Customers
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(x => x.TenantId == tenantId.Value && x.Email != null && x.Email.ToLower() == normalizedEmail);
                if (customer is null)
                {
                    return BadRequest(new { message = "Unable to create buyer profile for selected tenant." });
                }
            }

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
