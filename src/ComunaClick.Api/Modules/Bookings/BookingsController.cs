using ComunaClick.Api.Modules.Bookings.Contracts;
using ComunaClick.Api.Persistence;
using ComunaClick.Api.Persistence.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace ComunaClick.Api.Modules.Bookings;

[ApiController]
[Authorize(Policy = "partner.staff")]
[Route("v1/bookings")]
public sealed class BookingsController : ControllerBase
{
    private readonly CoreDbContext _db;
    private readonly ITenantContext _tenantContext;

    public BookingsController(CoreDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Booking>> Get(Guid id)
    {
        var booking = await _db.Bookings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return booking is null ? NotFound() : Ok(booking);
    }

    [AllowAnonymous]
    [EnableRateLimiting("public-read")]
    [HttpGet("/v1/public/bookings/{id:guid}")]
    public async Task<ActionResult<object>> GetPublic(Guid id)
    {
        var booking = await _db.Bookings.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

        if (booking is null)
        {
            return NotFound();
        }

        var partner = await _db.Partners.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == booking.PartnerId);

        var service = await _db.Services.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == booking.ServiceId);

        object? professional = null;

        if (partner is not null && string.Equals(partner.Type, "C", StringComparison.OrdinalIgnoreCase))
        {
            var matchedProfessional = await _db.Professionals.AsNoTracking()
                .Where(x => x.IsActive && x.IsVerified && x.TenantId == partner.TenantId)
                .Where(x => x.ComunaId == partner.ComunaId || (x.ComunaId == null && partner.ComunaId == null))
                .OrderBy(x => x.Name)
                .FirstOrDefaultAsync();

            if (matchedProfessional is not null)
            {
                professional = new
                {
                    matchedProfessional.Id,
                    matchedProfessional.Name,
                    matchedProfessional.Specialty,
                    matchedProfessional.Email,
                    matchedProfessional.Phone
                };
            }
        }

        return Ok(new
        {
            booking.Id,
            booking.Status,
            booking.StartAt,
            booking.EndAt,
            booking.Amount,
            booking.Currency,
            booking.CancellationPolicy,
            booking.CreatedAt,
            booking.UpdatedAt,
            Partner = partner is null ? null : new
            {
                partner.Id,
                partner.Name,
                partner.Address,
                partner.Phone,
                partner.Email
            },
            Service = service is null ? null : new
            {
                service.Id,
                service.Name,
                service.Description,
                service.Category,
                service.DurationMinutes
            },
            Professional = professional
        });
    }

    [HttpGet("/v1/partners/{partnerId:guid}/bookings")]
    public async Task<ActionResult<IEnumerable<Booking>>> ListByPartner(Guid partnerId)
    {
        var scopedPartner = _tenantContext.PartnerId;
        if (scopedPartner.HasValue && scopedPartner.Value != partnerId)
        {
            return Forbid();
        }

        var bookings = await _db.Bookings.AsNoTracking()
            .Where(x => x.PartnerId == partnerId)
            .OrderByDescending(x => x.StartAt)
            .ToListAsync();
        return Ok(bookings);
    }

    [HttpPost]
    public async Task<ActionResult<Booking>> Create(BookingCreateRequest request)
    {
        var tenantId = _tenantContext.TenantId;
        if (!tenantId.HasValue)
        {
            return BadRequest(new { message = "TenantId is required." });
        }

        var partnerId = _tenantContext.PartnerId ?? request.PartnerId;
        if (partnerId == Guid.Empty)
        {
            return BadRequest(new { message = "PartnerId is required." });
        }

        if (_tenantContext.PartnerId.HasValue && request.PartnerId != Guid.Empty && request.PartnerId != _tenantContext.PartnerId.Value)
        {
            return Forbid();
        }

        if (request.EndAt <= request.StartAt)
        {
            return BadRequest(new { message = "EndAt must be after StartAt." });
        }

        var booking = new Booking
        {
            TenantId = tenantId.Value,
            PartnerId = partnerId,
            ServiceId = request.ServiceId,
            SlotId = request.SlotId,
            CustomerId = request.CustomerId,
            Status = "payment_pending",
            StartAt = request.StartAt,
            EndAt = request.EndAt,
            Amount = request.Amount,
            Currency = string.IsNullOrWhiteSpace(request.Currency) ? "CLP" : request.Currency.Trim(),
            CancellationPolicy = string.IsNullOrWhiteSpace(request.CancellationPolicy) ? "{}" : request.CancellationPolicy,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _db.Bookings.Add(booking);
        await _db.SaveChangesAsync();
        return Created($"/v1/bookings/{booking.Id}", booking);
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<Booking>> UpdateStatus(Guid id, BookingStatusUpdateRequest request)
    {
        var booking = await _db.Bookings.FirstOrDefaultAsync(x => x.Id == id);
        if (booking is null)
        {
            return NotFound();
        }

        booking.Status = request.Status.Trim();
        booking.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(booking);
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<Booking>> Cancel(Guid id)
    {
        var booking = await _db.Bookings.FirstOrDefaultAsync(x => x.Id == id);
        if (booking is null)
        {
            return NotFound();
        }

        booking.Status = "cancelled";
        booking.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(booking);
    }
}
