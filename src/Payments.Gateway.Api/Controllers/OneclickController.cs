using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Payments.Gateway.Api.Contracts.Oneclick;
using Payments.Gateway.Api.Persistence;
using Payments.Gateway.Api.Persistence.Entities;

namespace Payments.Gateway.Api.Controllers;

[ApiController]
[Route("v1/oneclick")]
public sealed class OneclickController : ControllerBase
{
    private readonly PaymentsDbContext _db;

    public OneclickController(PaymentsDbContext db)
    {
        _db = db;
    }

    [HttpPost("enrollments")]
    public async Task<ActionResult<EnrollmentResponse>> Enroll(EnrollmentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CustomerId))
        {
            return BadRequest(new { message = "CustomerId is required." });
        }

        var providerRef = string.IsNullOrWhiteSpace(request.ProviderRef)
            ? $"oc_{Guid.NewGuid():N}"
            : request.ProviderRef.Trim();

        var exists = await _db.CustomerTokens.AnyAsync(x =>
            x.CustomerId == request.CustomerId && x.ProviderRef == providerRef);
        if (exists)
        {
            return Conflict(new { message = "Customer token already exists." });
        }

        var token = new CustomerToken
        {
            CustomerId = request.CustomerId,
            ProviderRef = providerRef,
            Status = "active",
            RawResponse = string.IsNullOrWhiteSpace(request.RawPayload) ? "{}" : request.RawPayload,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.CustomerTokens.Add(token);
        await _db.SaveChangesAsync();

        return Created($"/v1/oneclick/enrollments/{token.Id}", new EnrollmentResponse(token.Id, token.CustomerId, token.ProviderRef, token.Status));
    }

    [HttpPost("charges")]
    public async Task<ActionResult<ChargeResponse>> Charge(ChargeRequest request)
    {
        var token = await _db.CustomerTokens.FirstOrDefaultAsync(x => x.Id == request.CustomerTokenId);
        if (token is null || token.Status != "active")
        {
            return BadRequest(new { message = "Customer token is invalid or inactive." });
        }

        var intent = await _db.PaymentIntents.FirstOrDefaultAsync(x => x.Id == request.IntentId);
        if (intent is null)
        {
            return BadRequest(new { message = "Payment intent not found." });
        }

        if (request.Amount <= 0)
        {
            return BadRequest(new { message = "Amount must be greater than zero." });
        }

        var charge = new Charge
        {
            CustomerTokenId = token.Id,
            IntentId = intent.Id,
            Amount = request.Amount,
            Currency = string.IsNullOrWhiteSpace(request.Currency) ? "CLP" : request.Currency.Trim(),
            Status = "pending",
            ProviderRef = request.ProviderRef,
            RawResponse = string.IsNullOrWhiteSpace(request.RawPayload) ? "{}" : request.RawPayload,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.Charges.Add(charge);
        await _db.SaveChangesAsync();

        return Created($"/v1/oneclick/charges/{charge.Id}", new ChargeResponse(charge.Id, charge.Status, charge.ProviderRef, charge.AuthorizationCode));
    }
}
