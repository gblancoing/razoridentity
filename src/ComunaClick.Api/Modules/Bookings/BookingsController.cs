using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
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
    private static readonly HashSet<string> AllowedWorkflowStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "pending",
        "payment_pending",
        "confirmed",
        "completed",
        "no_show",
        "cancelled"
    };

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
        var tenantId = _tenantContext.TenantId;
        if (!tenantId.HasValue)
        {
            return BadRequest(new { message = "TenantId is required." });
        }

        var booking = await _db.Bookings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId.Value);
        if (booking is null)
        {
            return NotFound();
        }

        if (_tenantContext.PartnerId.HasValue && _tenantContext.PartnerId.Value != booking.PartnerId)
        {
            return Forbid();
        }

        return Ok(booking);
    }

    [AllowAnonymous]
    [EnableRateLimiting("public-read")]
    [HttpGet("/v1/public/bookings/{id:guid}")]
    public async Task<ActionResult<object>> GetPublic(Guid id, [FromQuery] Guid customerId)
    {
        if (customerId == Guid.Empty)
        {
            return NotFound();
        }

        var booking = await _db.Bookings.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.CustomerId == customerId);

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
                partner.Phone
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
        var tenantId = _tenantContext.TenantId;
        if (!tenantId.HasValue)
        {
            return BadRequest(new { message = "TenantId is required." });
        }

        var scopedPartner = _tenantContext.PartnerId;
        if (scopedPartner.HasValue && scopedPartner.Value != partnerId)
        {
            return Forbid();
        }

        var hasPartner = await _db.Partners.AsNoTracking()
            .AnyAsync(x => x.Id == partnerId && x.TenantId == tenantId.Value);
        if (!hasPartner)
        {
            return NotFound();
        }

        var bookings = await _db.Bookings.AsNoTracking()
            .Where(x => x.TenantId == tenantId.Value && x.PartnerId == partnerId)
            .OrderByDescending(x => x.StartAt)
            .ToListAsync();
        return Ok(bookings);
    }

    [Authorize(Policy = "buyer.customer")]
    [HttpGet("/v1/buyer/bookings/recent")]
    public async Task<ActionResult<IEnumerable<object>>> ListRecentForBuyer([FromQuery] int limit = 8)
    {
        var email = ResolveEmail(User);
        if (string.IsNullOrWhiteSpace(email))
        {
            return BadRequest(new { message = "Authenticated email claim is required." });
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var safeLimit = Math.Clamp(limit, 1, 30);

        var customerIds = await _db.Customers.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(x => x.Email != null && x.Email.ToLower() == normalizedEmail)
            .Select(x => x.Id)
            .ToListAsync();

        if (customerIds.Count == 0)
        {
            return Ok(Array.Empty<object>());
        }

        var bookings = await _db.Bookings.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(x => customerIds.Contains(x.CustomerId))
            .OrderByDescending(x => x.StartAt)
            .Take(safeLimit)
            .ToListAsync();

        if (bookings.Count == 0)
        {
            return Ok(Array.Empty<object>());
        }

        var partnerIds = bookings.Select(x => x.PartnerId).Distinct().ToList();
        var serviceIds = bookings.Select(x => x.ServiceId).Distinct().ToList();

        var partners = await _db.Partners.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(x => partnerIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id);

        var services = await _db.Services.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(x => serviceIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id);

        var response = bookings.Select(booking =>
        {
            partners.TryGetValue(booking.PartnerId, out var partner);
            services.TryGetValue(booking.ServiceId, out var service);

            return new
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
                    partner.Phone
                },
                Service = service is null ? null : new
                {
                    service.Id,
                    service.Name,
                    service.Description,
                    service.Category,
                    service.DurationMinutes
                },
                Professional = (object?)null
            };
        }).ToList();

        return Ok(response);
    }

    [Authorize(Policy = "buyer.customer")]
    [EnableRateLimiting("public-write")]
    [HttpPost]
    public async Task<ActionResult<Booking>> Create(BookingCreateRequest request)
    {
        var service = await _db.Services.AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == request.ServiceId && x.IsActive);

        if (service is null)
        {
            return BadRequest(new { message = "Selected service is not available for booking." });
        }

        var tenantId = service.TenantId;
        var customerId = await ResolveOrEnsureCustomerIdAsync(request.CustomerId, tenantId);
        if (!customerId.HasValue)
        {
            return BadRequest(new { message = "CustomerId does not exist for selected service tenant and buyer profile could not be resolved." });
        }

        if (request.EndAt <= request.StartAt)
        {
            return BadRequest(new { message = "EndAt must be after StartAt." });
        }

        if (request.PartnerId != Guid.Empty && request.PartnerId != service.PartnerId)
        {
            return BadRequest(new { message = "PartnerId does not match selected service." });
        }

        var hasVisiblePartner = await _db.Partners.AsNoTracking()
            .IgnoreQueryFilters()
            .AnyAsync(x => x.Id == service.PartnerId && x.TenantId == tenantId && x.IsVisible);

        if (!hasVisiblePartner)
        {
            return BadRequest(new { message = "Selected partner is not publicly available." });
        }

        ServiceSlot? slot = null;
        if (request.SlotId.HasValue)
        {
            slot = await _db.ServiceSlots
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x =>
                    x.Id == request.SlotId.Value &&
                    x.TenantId == tenantId &&
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
            TenantId = tenantId,
            PartnerId = service.PartnerId,
            ServiceId = service.Id,
            SlotId = request.SlotId,
            CustomerId = customerId.Value,
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
        var tenantId = _tenantContext.TenantId;
        if (!tenantId.HasValue)
        {
            return BadRequest(new { message = "TenantId is required." });
        }

        var booking = await _db.Bookings.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId.Value);
        if (booking is null)
        {
            return NotFound();
        }

        if (_tenantContext.PartnerId.HasValue && _tenantContext.PartnerId.Value != booking.PartnerId)
        {
            return Forbid();
        }

        if (!TryNormalizeStatus(request.Status, out var normalizedStatus, out var validationError))
        {
            return BadRequest(new { message = validationError });
        }

        if (normalizedStatus is "no_show" or "cancelled")
        {
            return BadRequest(new { message = "Use workflow endpoint to set no_show or cancelled with reason." });
        }

        booking.Status = normalizedStatus;
        booking.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(booking);
    }

    [Authorize(Policy = "partner.staff")]
    [HttpPatch("{id:guid}/workflow")]
    public async Task<ActionResult<Booking>> UpdateWorkflow(Guid id, BookingWorkflowUpdateRequest request)
    {
        var tenantId = _tenantContext.TenantId;
        if (!tenantId.HasValue)
        {
            return BadRequest(new { message = "TenantId is required." });
        }

        var booking = await _db.Bookings.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId.Value);
        if (booking is null)
        {
            return NotFound();
        }

        if (_tenantContext.PartnerId.HasValue && _tenantContext.PartnerId.Value != booking.PartnerId)
        {
            return Forbid();
        }

        if (request.Status is not null)
        {
            if (!TryNormalizeStatus(request.Status, out var normalizedStatus, out var validationError))
            {
                return BadRequest(new { message = validationError });
            }

            booking.Status = normalizedStatus;
        }

        if (request.StartAt.HasValue)
        {
            booking.StartAt = request.StartAt.Value;
        }

        if (request.EndAt.HasValue)
        {
            booking.EndAt = request.EndAt.Value;
        }

        if (booking.EndAt <= booking.StartAt)
        {
            return BadRequest(new { message = "EndAt must be after StartAt." });
        }

        if (request.InternalNote is not null)
        {
            booking.InternalNote = string.IsNullOrWhiteSpace(request.InternalNote) ? null : request.InternalNote.Trim();
        }

        if (request.OutcomeReason is not null)
        {
            var reason = string.IsNullOrWhiteSpace(request.OutcomeReason) ? null : request.OutcomeReason.Trim();
            booking.OutcomeReason = reason is { Length: > 240 } ? reason[..240] : reason;
        }

        if (booking.Status is "cancelled" or "no_show" &&
            string.IsNullOrWhiteSpace(booking.OutcomeReason) &&
            (request.Status is not null || request.OutcomeReason is not null))
        {
            return BadRequest(new { message = "OutcomeReason is required for cancelled or no_show status." });
        }

        booking.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(booking);
    }

    [Authorize(Policy = "partner.staff")]
    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<Booking>> Cancel(Guid id)
    {
        var tenantId = _tenantContext.TenantId;
        if (!tenantId.HasValue)
        {
            return BadRequest(new { message = "TenantId is required." });
        }

        var booking = await _db.Bookings.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId.Value);
        if (booking is null)
        {
            return NotFound();
        }

        if (_tenantContext.PartnerId.HasValue && _tenantContext.PartnerId.Value != booking.PartnerId)
        {
            return Forbid();
        }

        booking.Status = "cancelled";
        booking.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(booking);
    }

    private static bool TryNormalizeStatus(string? rawStatus, out string normalizedStatus, out string validationError)
    {
        normalizedStatus = string.Empty;
        validationError = string.Empty;

        if (string.IsNullOrWhiteSpace(rawStatus))
        {
            validationError = "Status is required.";
            return false;
        }

        normalizedStatus = rawStatus.Trim().ToLowerInvariant();
        if (normalizedStatus == "canceled")
        {
            normalizedStatus = "cancelled";
        }

        if (!AllowedWorkflowStatuses.Contains(normalizedStatus))
        {
            validationError = "Status is not valid.";
            return false;
        }

        return true;
    }

    private async Task<Guid?> ResolveOrEnsureCustomerIdAsync(Guid requestedCustomerId, Guid tenantId)
    {
        if (requestedCustomerId != Guid.Empty)
        {
            var hasRequestedCustomer = await _db.Customers.AsNoTracking()
                .IgnoreQueryFilters()
                .AnyAsync(x => x.Id == requestedCustomerId && x.TenantId == tenantId);
            if (hasRequestedCustomer)
            {
                return requestedCustomerId;
            }
        }

        var email = ResolveEmail(User);
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var customer = await _db.Customers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Email != null && x.Email.ToLower() == normalizedEmail);

        if (customer is null)
        {
            customer = new Customer
            {
                TenantId = tenantId,
                Email = normalizedEmail,
                FullName = ResolveName(User),
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
                    .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Email != null && x.Email.ToLower() == normalizedEmail);
                if (customer is null)
                {
                    return null;
                }
            }
        }

        return customer.Id;
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
