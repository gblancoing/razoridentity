using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ComunaClick.Api.Modules.Inbox.Contracts;
using ComunaClick.Api.Persistence;
using ComunaClick.Api.Persistence.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace ComunaClick.Api.Modules.Inbox;

[ApiController]
[Route("v1/inbox")]
public sealed class InboxController : ControllerBase
{
    private readonly CoreDbContext _db;
    private readonly ITenantContext _tenantContext;

    public InboxController(CoreDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    [Authorize(Policy = "buyer.customer")]
    [HttpGet("threads/mine")]
    public async Task<ActionResult<IEnumerable<object>>> ListMineForCustomer(
        [FromQuery] string? folder = "inbox",
        [FromQuery] int limit = 50)
    {
        limit = Math.Clamp(limit, 1, 100);
        var email = ResolveEmail(User);
        if (string.IsNullOrWhiteSpace(email))
        {
            return BadRequest(new { message = "Authenticated email is required." });
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var customerIds = await _db.Customers.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.Email != null && x.Email.ToLower() == normalizedEmail)
            .Select(x => x.Id)
            .ToListAsync();

        if (customerIds.Count == 0)
        {
            return Ok(Array.Empty<object>());
        }

        var archived = string.Equals(folder, "archived", StringComparison.OrdinalIgnoreCase);
        var threads = await BuildThreadListQuery()
            .Where(x => customerIds.Contains(x.CustomerId))
            .Where(x => archived ? x.Status == "archived" : x.Status != "archived")
            .OrderByDescending(x => x.LastMessageAt)
            .Take(limit)
            .ToListAsync();

        return Ok(threads.Select(x => MapThreadListItem(x, "customer")));
    }

    [Authorize(Policy = "partner.staff")]
    [HttpGet("threads/partner/{partnerId:guid}")]
    public async Task<ActionResult<IEnumerable<object>>> ListForPartner(
        Guid partnerId,
        [FromQuery] string? folder = "inbox",
        [FromQuery] int limit = 50)
    {
        limit = Math.Clamp(limit, 1, 100);
        var tenantId = _tenantContext.TenantId;
        if (!tenantId.HasValue)
        {
            return BadRequest(new { message = "TenantId is required." });
        }

        if (!await CanAccessPartnerAsync(partnerId, tenantId.Value))
        {
            return Forbid();
        }

        var professionalIds = await _db.Professionals.AsNoTracking()
            .Where(x => x.TenantId == tenantId.Value)
            .Where(x => _db.Partners.Any(p => p.Id == partnerId && (p.ComunaId == null || x.ComunaId == p.ComunaId)))
            .Select(x => x.Id)
            .ToListAsync();

        var archived = string.Equals(folder, "archived", StringComparison.OrdinalIgnoreCase);
        var threads = await BuildThreadListQuery()
            .Where(x => x.TenantId == tenantId.Value)
            .Where(x => x.PartnerId == partnerId || (x.ProfessionalId.HasValue && professionalIds.Contains(x.ProfessionalId.Value)))
            .Where(x => archived ? x.Status == "archived" : x.Status != "archived")
            .OrderByDescending(x => x.LastMessageAt)
            .Take(limit)
            .ToListAsync();

        return Ok(threads.Select(x => MapThreadListItem(x, "business")));
    }

    [Authorize]
    [HttpGet("threads/{id:guid}")]
    public async Task<ActionResult<object>> GetThread(Guid id)
    {
        var thread = await _db.InboxThreads.AsNoTracking()
            .Include(x => x.Customer)
            .Include(x => x.Partner)
            .Include(x => x.Professional)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (thread is null)
        {
            return NotFound();
        }

        var role = await ResolveViewerRoleAsync(thread);
        if (role is null)
        {
            return Forbid();
        }

        var messages = await _db.InboxMessages.AsNoTracking()
            .Where(x => x.ThreadId == id)
            .OrderBy(x => x.CreatedAt)
            .Select(x => new
            {
                x.Id,
                x.SenderRole,
                x.Body,
                x.CreatedAt
            })
            .ToListAsync();

        return Ok(new
        {
            Thread = MapThreadDetail(thread, role),
            Messages = messages
        });
    }

    [Authorize(Policy = "buyer.customer")]
    [HttpPost("threads")]
    [EnableRateLimiting("public-write")]
    public async Task<ActionResult<object>> CreateThread(InboxThreadCreateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Body))
        {
            return BadRequest(new { message = "Message body is required." });
        }

        var subject = string.IsNullOrWhiteSpace(request.Subject)
            ? "Consulta desde ComunaClic"
            : request.Subject.Trim()[..Math.Min(240, request.Subject.Trim().Length)];

        var hasPartner = request.PartnerId is Guid partnerId && partnerId != Guid.Empty;
        var hasProfessional = request.ProfessionalId is Guid professionalId && professionalId != Guid.Empty;
        if (!hasPartner && !hasProfessional)
        {
            return BadRequest(new { message = "PartnerId or ProfessionalId is required." });
        }

        Guid tenantId;
        Guid? resolvedPartnerId = null;
        Guid? resolvedProfessionalId = null;

        if (hasProfessional)
        {
            var professional = await _db.Professionals.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == request.ProfessionalId!.Value && x.IsActive);
            if (professional is null)
            {
                return BadRequest(new { message = "Professional is not available." });
            }

            tenantId = professional.TenantId;
            resolvedProfessionalId = professional.Id;
            if (hasPartner)
            {
                resolvedPartnerId = request.PartnerId;
            }
        }
        else
        {
            var partner = await _db.Partners.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == request.PartnerId!.Value && x.IsVisible);
            if (partner is null)
            {
                return BadRequest(new { message = "Business is not available for contact." });
            }

            tenantId = partner.TenantId;
            resolvedPartnerId = partner.Id;
        }

        var email = ResolveEmail(User);
        if (string.IsNullOrWhiteSpace(email))
        {
            return BadRequest(new { message = "Authenticated email is required." });
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var customer = await _db.Customers.IgnoreQueryFilters()
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
            await _db.SaveChangesAsync();
        }

        var existing = await FindOpenThreadAsync(customer.Id, resolvedPartnerId, resolvedProfessionalId);
        if (existing is not null)
        {
            var reply = await AppendMessageAsync(existing, "customer", request.Body.Trim());
            return Ok(new { ThreadId = existing.Id, Reused = true, Message = reply });
        }

        var now = DateTimeOffset.UtcNow;
        var thread = new InboxThread
        {
            TenantId = tenantId,
            CustomerId = customer.Id,
            PartnerId = resolvedPartnerId,
            ProfessionalId = resolvedProfessionalId,
            Subject = subject,
            Status = "open",
            LastMessageAt = now,
            CustomerLastReadAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.InboxThreads.Add(thread);
        await LinkCustomerPartnerAsync(tenantId, customer.Id, resolvedPartnerId, now);
        await _db.SaveChangesAsync();

        var message = new InboxMessage
        {
            ThreadId = thread.Id,
            SenderRole = "customer",
            Body = request.Body.Trim(),
            CreatedAt = now
        };
        _db.InboxMessages.Add(message);
        await _db.SaveChangesAsync();

        return Created($"/v1/inbox/threads/{thread.Id}", new { ThreadId = thread.Id, Reused = false });
    }

    [Authorize]
    [HttpPost("threads/{id:guid}/messages")]
    [EnableRateLimiting("public-write")]
    public async Task<ActionResult<object>> Reply(Guid id, InboxMessageCreateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Body))
        {
            return BadRequest(new { message = "Message body is required." });
        }

        var thread = await _db.InboxThreads.FirstOrDefaultAsync(x => x.Id == id);
        if (thread is null)
        {
            return NotFound();
        }

        var role = await ResolveViewerRoleAsync(thread);
        if (role is null)
        {
            return Forbid();
        }

        if (thread.Status == "archived")
        {
            thread.Status = "open";
        }

        var message = await AppendMessageAsync(thread, role, request.Body.Trim());
        return Ok(message);
    }

    [Authorize]
    [HttpPatch("threads/{id:guid}/read")]
    public async Task<ActionResult> MarkRead(Guid id)
    {
        var thread = await _db.InboxThreads.FirstOrDefaultAsync(x => x.Id == id);
        if (thread is null)
        {
            return NotFound();
        }

        var role = await ResolveViewerRoleAsync(thread);
        if (role is null)
        {
            return Forbid();
        }

        var now = DateTimeOffset.UtcNow;
        if (role == "customer")
        {
            thread.CustomerLastReadAt = now;
        }
        else
        {
            thread.PartnerLastReadAt = now;
        }

        thread.UpdatedAt = now;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [Authorize]
    [HttpPatch("threads/{id:guid}/status")]
    public async Task<ActionResult<object>> UpdateStatus(Guid id, InboxThreadStatusRequest request)
    {
        var thread = await _db.InboxThreads.FirstOrDefaultAsync(x => x.Id == id);
        if (thread is null)
        {
            return NotFound();
        }

        var role = await ResolveViewerRoleAsync(thread);
        if (role is null)
        {
            return Forbid();
        }

        var status = request.Status?.Trim().ToLowerInvariant();
        if (status is not ("open" or "archived"))
        {
            return BadRequest(new { message = "Status must be open or archived." });
        }

        thread.Status = status;
        thread.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { thread.Id, thread.Status });
    }

    private IQueryable<InboxThread> BuildThreadListQuery()
        => _db.InboxThreads.AsNoTracking()
            .Include(x => x.Customer)
            .Include(x => x.Partner)
            .Include(x => x.Professional)
            .Include(x => x.Messages);

    private static object MapThreadListItem(InboxThread thread, string viewerRole)
    {
        var lastMessage = thread.Messages.OrderByDescending(m => m.CreatedAt).FirstOrDefault();
        var lastPreview = lastMessage?.Body ?? string.Empty;
        var unread = viewerRole == "customer"
            ? thread.Messages.Any(m => m.SenderRole == "business" && (thread.CustomerLastReadAt == null || m.CreatedAt > thread.CustomerLastReadAt))
            : thread.Messages.Any(m => m.SenderRole == "customer" && (thread.PartnerLastReadAt == null || m.CreatedAt > thread.PartnerLastReadAt));

        return new
        {
            thread.Id,
            thread.Subject,
            thread.Status,
            thread.LastMessageAt,
            Preview = TruncatePreview(lastPreview),
            Unread = unread,
            CounterpartyName = ResolveCounterpartyName(thread, viewerRole),
            CounterpartySubtitle = ResolveCounterpartySubtitle(thread, viewerRole),
            thread.PartnerId,
            thread.ProfessionalId
        };
    }

    private static object MapThreadDetail(InboxThread thread, string viewerRole)
        => new
        {
            thread.Id,
            thread.Subject,
            thread.Status,
            thread.LastMessageAt,
            CounterpartyName = ResolveCounterpartyName(thread, viewerRole),
            CounterpartySubtitle = ResolveCounterpartySubtitle(thread, viewerRole),
            CustomerName = thread.Customer.FullName ?? thread.Customer.Email,
            thread.PartnerId,
            thread.ProfessionalId,
            CustomerPhone = thread.Customer.Phone,
            PartnerPhone = thread.Partner?.Phone,
            ProfessionalPhone = thread.Professional?.Phone
        };

    private static string ResolveCounterpartyName(InboxThread thread, string viewerRole)
    {
        if (viewerRole == "customer")
        {
            if (thread.Partner is not null)
            {
                return thread.Partner.Name;
            }

            return thread.Professional?.Name ?? "Destinatario";
        }

        return thread.Customer.FullName ?? thread.Customer.Email ?? "Cliente";
    }

    private static string? ResolveCounterpartySubtitle(InboxThread thread, string viewerRole)
    {
        if (viewerRole == "customer")
        {
            if (thread.Professional is not null)
            {
                return thread.Professional.Specialty;
            }

            return thread.Partner?.Type switch
            {
                "B" => "Empresa de servicios",
                "A" => "Comercio",
                "C" => "Profesionales",
                _ => null
            };
        }

        if (thread.Professional is not null)
        {
            return $"Profesional · {thread.Professional.Specialty}";
        }

        return thread.Partner?.Type switch
        {
            "B" => "Consulta a negocio",
            _ => "Consulta"
        };
    }

    private static string TruncatePreview(string value)
    {
        var trimmed = value.Replace('\n', ' ').Trim();
        return trimmed.Length <= 120 ? trimmed : trimmed[..117] + "...";
    }

    private async Task<InboxThread?> FindOpenThreadAsync(Guid customerId, Guid? partnerId, Guid? professionalId)
    {
        return await _db.InboxThreads
            .Where(x => x.CustomerId == customerId && x.Status == "open")
            .Where(x => x.PartnerId == partnerId)
            .Where(x => x.ProfessionalId == professionalId)
            .OrderByDescending(x => x.LastMessageAt)
            .FirstOrDefaultAsync();
    }

    private async Task<object> AppendMessageAsync(InboxThread thread, string senderRole, string body)
    {
        var now = DateTimeOffset.UtcNow;
        var message = new InboxMessage
        {
            ThreadId = thread.Id,
            SenderRole = senderRole,
            Body = body,
            CreatedAt = now
        };

        thread.LastMessageAt = now;
        thread.UpdatedAt = now;
        if (senderRole == "customer")
        {
            thread.CustomerLastReadAt = now;
        }
        else
        {
            thread.PartnerLastReadAt = now;
        }

        _db.InboxMessages.Add(message);
        await _db.SaveChangesAsync();

        return new
        {
            message.Id,
            message.SenderRole,
            message.Body,
            message.CreatedAt
        };
    }

    private async Task LinkCustomerPartnerAsync(Guid tenantId, Guid customerId, Guid? partnerId, DateTimeOffset now)
    {
        if (!partnerId.HasValue)
        {
            return;
        }

        var link = await _db.CustomerPartnerLinks
            .FirstOrDefaultAsync(x => x.CustomerId == customerId && x.PartnerId == partnerId.Value);

        if (link is null)
        {
            _db.CustomerPartnerLinks.Add(new CustomerPartnerLink
            {
                TenantId = tenantId,
                CustomerId = customerId,
                PartnerId = partnerId.Value,
                FirstSeenAt = now,
                LastSeenAt = now,
                Source = "message"
            });
        }
        else
        {
            link.LastSeenAt = now;
            link.Source = "message";
        }
    }

    private async Task<string?> ResolveViewerRoleAsync(InboxThread thread)
    {
        if (User.IsInRole("partner") || User.HasClaim(c => c.Type == "partner_id"))
        {
            if (!await CanAccessPartnerForThreadAsync(thread))
            {
                return null;
            }

            return "business";
        }

        var email = ResolveEmail(User);
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var isCustomer = await _db.Customers.IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(x => x.Id == thread.CustomerId && x.Email != null && x.Email.ToLower() == normalizedEmail);

        return isCustomer ? "customer" : null;
    }

    private async Task<bool> CanAccessPartnerForThreadAsync(InboxThread thread)
    {
        var tenantId = _tenantContext.TenantId;
        if (!tenantId.HasValue || thread.TenantId != tenantId.Value)
        {
            return false;
        }

        var scopedPartner = _tenantContext.PartnerId;
        if (!scopedPartner.HasValue)
        {
            return true;
        }

        if (thread.PartnerId == scopedPartner.Value)
        {
            return true;
        }

        if (!thread.ProfessionalId.HasValue)
        {
            return false;
        }

        var partner = await _db.Partners.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == scopedPartner.Value && x.TenantId == tenantId.Value);

        if (partner is null)
        {
            return false;
        }

        return await _db.Professionals.AsNoTracking()
            .AnyAsync(x => x.Id == thread.ProfessionalId.Value && x.TenantId == tenantId.Value
                && (x.ComunaId == partner.ComunaId || (!x.ComunaId.HasValue && !partner.ComunaId.HasValue)));
    }

    private async Task<bool> CanAccessPartnerAsync(Guid partnerId, Guid tenantId)
    {
        var scopedPartner = _tenantContext.PartnerId;
        if (scopedPartner.HasValue && scopedPartner.Value != partnerId)
        {
            return false;
        }

        return await _db.Partners.AsNoTracking()
            .AnyAsync(x => x.Id == partnerId && x.TenantId == tenantId);
    }

    private static string? ResolveEmail(ClaimsPrincipal user)
        => user.FindFirst(JwtRegisteredClaimNames.Email)?.Value
            ?? user.FindFirst(ClaimTypes.Email)?.Value
            ?? user.FindFirst("email")?.Value;

    private static string? ResolveName(ClaimsPrincipal user)
        => user.FindFirst(ClaimTypes.Name)?.Value
            ?? user.FindFirst("name")?.Value
            ?? user.FindFirst(JwtRegisteredClaimNames.Name)?.Value;
}
