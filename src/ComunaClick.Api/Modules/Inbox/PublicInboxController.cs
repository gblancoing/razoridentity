using ComunaClick.Api.Integrations.Notifications;
using ComunaClick.Api.Modules.Inbox.Contracts;
using ComunaClick.Api.Persistence;
using ComunaClick.Api.Persistence.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace ComunaClick.Api.Modules.Inbox;

/// <summary>
/// Mensajes de visitantes sin cuenta hacia negocios/profesionales.
/// El visitante deja nombre y teléfono (correo opcional) para que el
/// destinatario pueda responderle por esos medios.
/// </summary>
[ApiController]
[Route("v1/public/inbox")]
public sealed class PublicInboxController : ControllerBase
{
    private readonly CoreDbContext _db;
    private readonly IInboxNotificationService _inboxNotifications;

    public PublicInboxController(CoreDbContext db, IInboxNotificationService inboxNotifications)
    {
        _db = db;
        _inboxNotifications = inboxNotifications;
    }

    [AllowAnonymous]
    [HttpPost("threads")]
    [EnableRateLimiting("public-write")]
    public async Task<ActionResult<object>> CreateGuestThread(GuestInboxThreadCreateRequest request)
    {
        var guestError = ValidateGuest(request);
        if (guestError is not null)
        {
            return BadRequest(new { message = guestError });
        }

        if (string.IsNullOrWhiteSpace(request.Body))
        {
            return BadRequest(new { message = "Escribí el mensaje que querés enviar." });
        }

        var hasPartner = request.PartnerId is Guid partnerIdValue && partnerIdValue != Guid.Empty;
        var hasProfessional = request.ProfessionalId is Guid professionalIdValue && professionalIdValue != Guid.Empty;
        if (!hasPartner && !hasProfessional)
        {
            return BadRequest(new { message = "Falta el destinatario del mensaje." });
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
                return BadRequest(new { message = "El profesional no está disponible para contacto." });
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
                return BadRequest(new { message = "El negocio no está disponible para contacto." });
            }

            tenantId = partner.TenantId;
            resolvedPartnerId = partner.Id;
        }

        var customer = await EnsureGuestCustomerAsync(tenantId, request);
        if (customer is null)
        {
            return BadRequest(new { message = "No se pudo registrar tu información de contacto." });
        }

        var subject = string.IsNullOrWhiteSpace(request.Subject)
            ? "Consulta desde ComunaClic"
            : request.Subject.Trim()[..Math.Min(240, request.Subject.Trim().Length)];
        var body = request.Body.Trim();
        var now = DateTimeOffset.UtcNow;

        // Misma regla que el inbox autenticado: si ya hay un hilo abierto con el
        // mismo destinatario, el mensaje se agrega ahí en lugar de duplicar hilos.
        var existing = await _db.InboxThreads
            .Where(x => x.CustomerId == customer.Id && x.Status == "open")
            .Where(x => x.PartnerId == resolvedPartnerId)
            .Where(x => x.ProfessionalId == resolvedProfessionalId)
            .OrderByDescending(x => x.LastMessageAt)
            .FirstOrDefaultAsync();

        if (existing is not null)
        {
            var replyMessage = new InboxMessage
            {
                ThreadId = existing.Id,
                SenderRole = "customer",
                Body = body,
                CreatedAt = now
            };
            existing.LastMessageAt = now;
            existing.UpdatedAt = now;
            existing.CustomerLastReadAt = now;
            _db.InboxMessages.Add(replyMessage);
            await _db.SaveChangesAsync();

            await _inboxNotifications.NotifyNewInboxMessageAsync(existing.Id, replyMessage.Id, "customer");
            return Ok(new { ThreadId = existing.Id, Reused = true });
        }

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
            Body = body,
            CreatedAt = now
        };
        _db.InboxMessages.Add(message);
        await _db.SaveChangesAsync();

        await _inboxNotifications.NotifyNewInboxMessageAsync(thread.Id, message.Id, "customer");

        return Created($"/v1/inbox/threads/{thread.Id}", new { ThreadId = thread.Id, Reused = false });
    }

    private static string? ValidateGuest(GuestInboxThreadCreateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FullName) || request.FullName.Trim().Length < 2)
        {
            return "Ingresá tu nombre para que puedan responderte.";
        }

        if (string.IsNullOrWhiteSpace(request.Phone) || request.Phone.Trim().Length < 6)
        {
            return "Ingresá un teléfono de contacto válido.";
        }

        if (!string.IsNullOrWhiteSpace(request.Email) && !request.Email.Contains('@'))
        {
            return "El correo ingresado no es válido.";
        }

        return null;
    }

    /// <summary>
    /// Crea o reutiliza el cliente invitado: por correo si lo dejó, si no por
    /// teléfono. El correo es opcional en mensajes (a diferencia de las órdenes).
    /// </summary>
    private async Task<Customer?> EnsureGuestCustomerAsync(Guid tenantId, GuestInboxThreadCreateRequest request)
    {
        var fullName = request.FullName.Trim();
        var phone = request.Phone.Trim();
        var normalizedEmail = string.IsNullOrWhiteSpace(request.Email)
            ? null
            : request.Email.Trim().ToLowerInvariant();

        Customer? customer = null;
        if (normalizedEmail is not null)
        {
            customer = await _db.Customers.IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Email != null && x.Email.ToLower() == normalizedEmail);
        }

        customer ??= await _db.Customers.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Phone != null && x.Phone == phone);

        if (customer is null)
        {
            customer = new Customer
            {
                TenantId = tenantId,
                Email = normalizedEmail,
                FullName = fullName,
                Phone = phone,
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
                customer = normalizedEmail is not null
                    ? await _db.Customers.IgnoreQueryFilters()
                        .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Email != null && x.Email.ToLower() == normalizedEmail)
                    : await _db.Customers.IgnoreQueryFilters()
                        .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Phone != null && x.Phone == phone);
            }

            return customer;
        }

        customer.FullName = fullName;
        customer.Phone = phone;
        if (normalizedEmail is not null && string.IsNullOrWhiteSpace(customer.Email))
        {
            customer.Email = normalizedEmail;
        }

        customer.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return customer;
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
}
