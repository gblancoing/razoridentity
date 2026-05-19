using ComunaClick.Api.Modules.Crm.Contracts;
using ComunaClick.Api.Persistence;
using ComunaClick.Api.Persistence.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ComunaClick.Api.Modules.Crm;

[ApiController]
[Authorize(Policy = "partner.staff")]
[Route("v1/customers")]
public sealed class CustomersController : ControllerBase
{
    private readonly CoreDbContext _db;
    private readonly ITenantContext _tenantContext;

    public CustomersController(CoreDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Customer>> Get(Guid id)
    {
        var customer = await _db.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return customer is null ? NotFound() : Ok(customer);
    }

    [HttpPost]
    public async Task<ActionResult<Customer>> Create(CustomerCreateRequest request)
    {
        var tenantId = _tenantContext.TenantId;
        if (!tenantId.HasValue)
        {
            return BadRequest(new { message = "TenantId is required." });
        }

        var customer = new Customer
        {
            TenantId = tenantId.Value,
            Email = request.Email,
            Phone = request.Phone,
            FullName = request.FullName,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();
        return Created($"/v1/customers/{customer.Id}", customer);
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<Customer>> Update(Guid id, CustomerUpdateRequest request)
    {
        var customer = await _db.Customers.FirstOrDefaultAsync(x => x.Id == id);
        if (customer is null)
        {
            return NotFound();
        }

        if (request.Email is not null)
        {
            customer.Email = request.Email;
        }

        if (request.Phone is not null)
        {
            customer.Phone = request.Phone;
        }

        if (request.FullName is not null)
        {
            customer.FullName = request.FullName;
        }

        if (request.AvatarUrl is not null)
        {
            customer.AvatarUrl = CustomerProfileHelper.SanitizeAvatarUrl(request.AvatarUrl);
        }

        customer.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(customer);
    }

    [HttpGet("{id:guid}/timeline")]
    public async Task<ActionResult<IEnumerable<Interaction>>> Timeline(Guid id)
    {
        var timeline = await _db.Interactions.AsNoTracking()
            .Where(x => x.CustomerId == id)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();
        return Ok(timeline);
    }

    [HttpGet("/v1/partners/{partnerId:guid}/customers")]
    public async Task<ActionResult<IEnumerable<Customer>>> ListByPartner(Guid partnerId)
    {
        var scopedPartner = _tenantContext.PartnerId;
        if (scopedPartner.HasValue && scopedPartner.Value != partnerId)
        {
            return Forbid();
        }

        var customerIds = await _db.CustomerPartnerLinks.AsNoTracking()
            .Where(x => x.PartnerId == partnerId)
            .Select(x => x.CustomerId)
            .Distinct()
            .ToListAsync();

        var customers = await _db.Customers.AsNoTracking()
            .Where(x => customerIds.Contains(x.Id))
            .ToListAsync();

        return Ok(customers);
    }
}
