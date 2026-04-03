using ComunaClick.Api.Modules.Bookings.Contracts;
using ComunaClick.Api.Persistence;
using ComunaClick.Api.Persistence.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace ComunaClick.Api.Modules.Bookings;

[ApiController]
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

    [Authorize(Policy = "partner.staff")]
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

    [Authorize(Policy = "partner.staff")]
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

    [Authorize(Policy = "buyer.customer")]
    [EnableRateLimiting("public-write")]
    [HttpPost]
    public async Task<ActionResult<Booking>> Create(BookingCreateRequest request)
    {
        var tenantId = _tenantContext.TenantId;
        if (!tenantId.HasValue)
        {
            return BadRequest(new { message = "TenantId is required." });
        }

        if (request.CustomerId == Guid.Empty)
        {
            return BadRequest(new { message = "CustomerId is required." });
        }

        var hasCustomer = await _db.Customers.AsNoTracking()
            .AnyAsync(x => x.Id == request.CustomerId && x.TenantId == tenantId.Value);

        if (!hasCustomer)
        {
            return BadRequest(new { message = "CustomerId does not exist for current tenant." });
        }

        if (request.EndAt <= request.StartAt)
        {
            return BadRequest(new { message = "EndAt must be after StartAt." });
        }

        var service = await _db.Services.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.ServiceId && x.TenantId == tenantId.Value && x.IsActive);

        if (service is null)
        {
            return BadRequest(new { message = "Selected service is not available for booking." });
        }

        if (request.PartnerId != Guid.Empty && request.PartnerId != service.PartnerId)
        {
            return BadRequest(new { message = "PartnerId does not match selected service." });
        }

        var hasVisiblePartner = await _db.Partners.AsNoTracking()
            .AnyAsync(x => x.Id == service.PartnerId && x.TenantId == tenantId.Value && x.IsVisible);

        if (!hasVisiblePartner)
        {
            return BadRequest(new { message = "Selected partner is not publicly available." });
        }

        ServiceSlot? slot = null;
        if (request.SlotId.HasValue)
        {
            slot = await _db.ServiceSlots
                .FirstOrDefaultAsync(x =>
                    x.Id == request.SlotId.Value &&
                    x.TenantId == tenantId.Value &&
                    x.PartnerId == service.PartnerId &&
                    x.ServiceId == service.Id);

            if (slot is null)
            {
                return BadRequest(new { message = "Selected slot does not belong to this service." });
            }

            if (!slot.IsAvailable || slot.Capacity <= 0)
            {
                return Conflict(new { message = "Selected slot is no longer available." });
            }

            if (slot.StartAt != request.StartAt || slot.EndAt != request.EndAt)
            {
                return BadRequest(new { message = "StartAt/EndAt must match the selected slot." });
            }

            slot.IsAvailable = false;
            slot.Capacity = 0;
        }

        var booking = new Booking
        {
            TenantId = tenantId.Value,
            PartnerId = service.PartnerId,
            ServiceId = service.Id,
            SlotId = request.SlotId,
            CustomerId = request.CustomerId,
            Status = "payment_pending",
            StartAt = request.StartAt,
            EndAt = request.EndAt,
            Amount = service.Price,
            Currency = string.IsNullOrWhiteSpace(request.Currency) ? service.Currency : request.Currency.Trim(),
            CancellationPolicy = string.IsNullOrWhiteSpace(request.CancellationPolicy) ? "{}" : request.CancellationPolicy,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _db.Bookings.Add(booking);
        await _db.SaveChangesAsync();
        return Created($"/v1/bookings/{booking.Id}", booking);
    }

    [Authorize(Policy = "partner.staff")]
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

    [Authorize(Policy = "partner.staff")]
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
