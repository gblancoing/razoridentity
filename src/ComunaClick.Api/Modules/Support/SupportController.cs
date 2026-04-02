using ComunaClick.Api.Modules.Support.Contracts;
using ComunaClick.Api.Persistence;
using ComunaClick.Api.Persistence.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ComunaClick.Api.Modules.Support;

[ApiController]
[AllowAnonymous]
[EnableRateLimiting("public-write")]
[Route("v1/support")]
public sealed class SupportController : ControllerBase
{
    private readonly CoreDbContext _db;
    private readonly ITenantContext _tenantContext;

    public SupportController(CoreDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    [HttpPost("tickets")]
    public async Task<ActionResult<SupportTicketResponse>> CreateTicket(SupportTicketRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;
        if (!tenantId.HasValue)
        {
            return BadRequest(new { message = "TenantId is required." });
        }

        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new { message = "Email and message are required." });
        }

        Customer? customer = null;
        if (request.CustomerId.HasValue && request.CustomerId.Value != Guid.Empty)
        {
            customer = await _db.Customers.FirstOrDefaultAsync(x => x.Id == request.CustomerId.Value, cancellationToken);
        }

        if (customer is null)
        {
            var email = request.Email.Trim().ToLowerInvariant();
            customer = await _db.Customers.FirstOrDefaultAsync(x => x.Email != null && x.Email.ToLower() == email, cancellationToken);
        }

        if (customer is null)
        {
            customer = new Customer
            {
                TenantId = tenantId.Value,
                Email = request.Email.Trim(),
                FullName = request.Name,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            _db.Customers.Add(customer);
        }

        var payload = JsonSerializer.Serialize(new
        {
            request.Name,
            request.Email,
            request.Topic,
            request.Message
        });

        var interaction = new Interaction
        {
            TenantId = tenantId.Value,
            CustomerId = customer.Id,
            PartnerId = request.PartnerId,
            Type = "support_ticket",
            ReferenceId = null,
            Payload = payload,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.Interactions.Add(interaction);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new SupportTicketResponse(interaction.Id));
    }
}
