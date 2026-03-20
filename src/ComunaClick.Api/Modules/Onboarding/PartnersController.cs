using System.Security.Claims;
using ComunaClick.Api.Modules.Onboarding.Contracts.Partners;
using ComunaClick.Api.Persistence;
using ComunaClick.Api.Persistence.Entities;
using ComunaClick.Common.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ComunaClick.Api.Modules.Onboarding;

[ApiController]
[Authorize]
[Route("v1/partners")]
public sealed class PartnersController : ControllerBase
{
    private readonly CoreDbContext _db;
    private readonly ITenantContext _tenantContext;

    public PartnersController(CoreDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    [HttpGet]
    [Authorize(Policy = "partner.owner")]
    public async Task<ActionResult<IEnumerable<object>>> List()
    {
        var partners = await _db.Partners.AsNoTracking()
            .Include(x => x.Subcategory)
            .ThenInclude(x => x!.Category)
            .ToListAsync();

        var items = new List<object>(partners.Count);
        foreach (var partner in partners)
        {
            var activation = await BuildActivationStatusAsync(partner);
            items.Add(ToPartnerResponse(partner, activation));
        }

        return Ok(items);
    }

    [HttpGet("mine")]
    public async Task<ActionResult<IEnumerable<object>>> Mine()
    {
        var tenantId = _tenantContext.TenantId ?? ResolveTenantIdFromUser();
        if (!tenantId.HasValue)
        {
            return BadRequest(new { message = "TenantId is required." });
        }

        var partnersQuery = _db.Partners.AsNoTracking()
            .Include(x => x.Subcategory)
            .ThenInclude(x => x!.Category)
            .Where(x => x.TenantId == tenantId.Value);

        var scopedPartnerId = _tenantContext.PartnerId ?? ResolvePartnerIdFromUser();
        if (scopedPartnerId.HasValue)
        {
            partnersQuery = partnersQuery.Where(x => x.Id == scopedPartnerId.Value);
        }

        var partners = await partnersQuery
            .OrderBy(x => x.Name)
            .ToListAsync();

        var items = new List<object>(partners.Count);
        foreach (var partner in partners)
        {
            var activation = await BuildActivationStatusAsync(partner);
            items.Add(ToPartnerResponse(partner, activation));
        }

        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "partner.owner")]
    public async Task<ActionResult<object>> Get(Guid id)
    {
        var partner = await _db.Partners.AsNoTracking()
            .Include(x => x.Subcategory)
            .ThenInclude(x => x!.Category)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (partner is null)
        {
            return NotFound();
        }

        var activation = await BuildActivationStatusAsync(partner);
        return Ok(ToPartnerResponse(partner, activation));
    }

    [HttpPost]
    public async Task<ActionResult<object>> Create(PartnerCreateRequest request)
    {
        var tenantId = _tenantContext.TenantId ?? ResolveTenantIdFromUser();
        if (!tenantId.HasValue)
        {
            return BadRequest(new { message = "TenantId is required." });
        }

        if (string.IsNullOrWhiteSpace(request.Type) || string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { message = "Type and name are required." });
        }

        if (string.Equals(request.Type?.Trim(), "A", StringComparison.OrdinalIgnoreCase) && !request.SubcategoryId.HasValue)
        {
            return BadRequest(new { message = "Subcategory is required for partner type A." });
        }

        ProductSubcategory? subcategory = null;
        if (request.SubcategoryId.HasValue)
        {
            subcategory = await _db.ProductSubcategories
                .Include(x => x.Category)
                .FirstOrDefaultAsync(x => x.Id == request.SubcategoryId.Value && x.IsActive);

            if (subcategory is null)
            {
                return BadRequest(new { message = "Selected subcategory does not exist." });
            }
        }

        var normalizedName = request.Name!.Trim();
        var normalizedType = request.Type!.Trim();

        var exists = await _db.Partners.AnyAsync(x => x.TenantId == tenantId.Value && x.Name == normalizedName);
        if (exists)
        {
            return Conflict(new { message = "Partner name already exists for this tenant." });
        }

        var partner = new Partner
        {
            TenantId = tenantId.Value,
            SubcategoryId = request.SubcategoryId,
            Type = normalizedType,
            Name = normalizedName,
            Rut = request.Rut,
            Address = request.Address,
            Phone = request.Phone,
            Email = request.Email,
            IsVisible = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _db.Partners.Add(partner);
        await _db.SaveChangesAsync();
        partner.Subcategory = subcategory;
        var activation = await BuildActivationStatusAsync(partner);
        return Created($"/v1/partners/{partner.Id}", ToPartnerResponse(partner, activation));
    }

    [HttpPatch("{id:guid}")]
    [Authorize(Policy = "partner.owner")]
    public async Task<ActionResult<Partner>> Update(Guid id, PartnerUpdateRequest request)
    {
        var partner = await _db.Partners.FirstOrDefaultAsync(x => x.Id == id);
        if (partner is null)
        {
            return NotFound();
        }

        var scopedPartner = _tenantContext.PartnerId;
        if (scopedPartner.HasValue && scopedPartner.Value != partner.Id)
        {
            return Forbid();
        }

        if (!string.IsNullOrWhiteSpace(request.Type))
        {
            partner.Type = request.Type.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            var exists = await _db.Partners.AnyAsync(x =>
                x.TenantId == partner.TenantId &&
                x.Name == request.Name &&
                x.Id != id);
            if (exists)
            {
                return Conflict(new { message = "Partner name already exists for this tenant." });
            }
            partner.Name = request.Name.Trim();
        }

        if (request.Rut is not null)
        {
            partner.Rut = request.Rut;
        }

        if (request.Address is not null)
        {
            partner.Address = request.Address;
        }

        if (request.Phone is not null)
        {
            partner.Phone = request.Phone;
        }

        if (request.Email is not null)
        {
            partner.Email = request.Email;
        }

        if (request.SubcategoryId.HasValue || (request.SubcategoryId is null && string.Equals(partner.Type, "A", StringComparison.OrdinalIgnoreCase)))
        {
            if (string.Equals(partner.Type, "A", StringComparison.OrdinalIgnoreCase) && !request.SubcategoryId.HasValue)
            {
                return BadRequest(new { message = "Subcategory is required for partner type A." });
            }

            if (request.SubcategoryId.HasValue)
            {
                var subcategoryExists = await _db.ProductSubcategories.AnyAsync(x => x.Id == request.SubcategoryId.Value && x.IsActive);
                if (!subcategoryExists)
                {
                    return BadRequest(new { message = "Selected subcategory does not exist." });
                }
            }

            partner.SubcategoryId = request.SubcategoryId;
        }

        if (request.IsVisible.HasValue)
        {
            var requestedVisibility = request.IsVisible.Value;
            if (requestedVisibility)
            {
                var activation = await BuildActivationStatusAsync(partner);
                if (!activation.CanPublish)
                {
                    return BadRequest(new
                    {
                        message = "Partner is not ready to be published yet.",
                        activation
                    });
                }
            }

            partner.IsVisible = requestedVisibility;
        }

        partner.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        await _db.Entry(partner).Reference(x => x.Subcategory).LoadAsync();
        if (partner.Subcategory is not null)
        {
            await _db.Entry(partner.Subcategory).Reference(x => x.Category).LoadAsync();
        }
        var updatedActivation = await BuildActivationStatusAsync(partner);
        return Ok(ToPartnerResponse(partner, updatedActivation));
    }

    [HttpPatch("{id:guid}/visibility")]
    [Authorize(Policy = "partner.owner")]
    public async Task<ActionResult<Partner>> UpdateVisibility(Guid id, PartnerVisibilityRequest request)
    {
        var partner = await _db.Partners.FirstOrDefaultAsync(x => x.Id == id);
        if (partner is null)
        {
            return NotFound();
        }

        var scopedPartner = _tenantContext.PartnerId;
        if (scopedPartner.HasValue && scopedPartner.Value != partner.Id)
        {
            return Forbid();
        }

        if (request.IsVisible)
        {
            var activation = await BuildActivationStatusAsync(partner);
            if (!activation.CanPublish)
            {
                return BadRequest(new
                {
                    message = "Partner is not ready to be published yet.",
                    activation
                });
            }
        }

        partner.IsVisible = request.IsVisible;
        partner.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        await _db.Entry(partner).Reference(x => x.Subcategory).LoadAsync();
        if (partner.Subcategory is not null)
        {
            await _db.Entry(partner.Subcategory).Reference(x => x.Category).LoadAsync();
        }
        var updatedActivation = await BuildActivationStatusAsync(partner);
        return Ok(ToPartnerResponse(partner, updatedActivation));
    }

    [HttpGet("{id:guid}/activation")]
    [Authorize(Policy = "partner.owner")]
    public async Task<ActionResult<PartnerActivationStatusResponse>> GetActivation(Guid id)
    {
        var partner = await _db.Partners
            .Include(x => x.Subcategory)
            .ThenInclude(x => x!.Category)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (partner is null)
        {
            return NotFound();
        }

        var scopedPartner = _tenantContext.PartnerId;
        if (scopedPartner.HasValue && scopedPartner.Value != partner.Id)
        {
            return Forbid();
        }

        var activation = await BuildActivationStatusAsync(partner);
        return Ok(activation);
    }

    [HttpPost("{id:guid}/staff")]
    [Authorize(Policy = "partner.owner")]
    public async Task<ActionResult<PartnerStaff>> AddStaff(Guid id, PartnerStaffCreateRequest request)
    {
        var tenantId = _tenantContext.TenantId ?? ResolveTenantIdFromUser();
        if (!tenantId.HasValue)
        {
            return BadRequest(new { message = "TenantId is required." });
        }

        var exists = await _db.PartnerStaff.AnyAsync(x => x.PartnerId == id && x.UserId == request.UserId);
        if (exists)
        {
            return Conflict(new { message = "User already linked to this partner." });
        }

        var staff = new PartnerStaff
        {
            TenantId = tenantId.Value,
            PartnerId = id,
            UserId = request.UserId,
            Role = string.IsNullOrWhiteSpace(request.Role) ? "staff" : request.Role.Trim(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.PartnerStaff.Add(staff);
        await _db.SaveChangesAsync();
        return Created($"/v1/partners/{id}/staff/{staff.Id}", staff);
    }

    private async Task<PartnerActivationStatusResponse> BuildActivationStatusAsync(Partner partner)
    {
        var normalizedType = NormalizeType(partner.Type);
        var checklist = new List<PartnerChecklistItemResponse>
        {
            BuildItem("identity", "Nombre del negocio", !string.IsNullOrWhiteSpace(partner.Name), "Define cómo verán tu negocio los clientes."),
            BuildItem("contact", "Canal de contacto", HasContactChannel(partner), "Agrega teléfono o email para que puedan contactarte."),
            BuildItem("address", "Dirección operativa", !string.IsNullOrWhiteSpace(partner.Address), "Agrega una dirección o punto de atención."),
            BuildItem("tax", "Identificación tributaria", !string.IsNullOrWhiteSpace(partner.Rut), "Ingresa RUT o identificación del negocio.")
        };

        switch (normalizedType)
        {
            case "A":
                checklist.Add(BuildItem("catalog", "Subcategoría de productos", partner.SubcategoryId.HasValue, "Selecciona la subcategoría principal para publicar tu oferta."));
                var productCount = await _db.Products.CountAsync(x => x.PartnerId == partner.Id && x.IsActive);
                checklist.Add(BuildItem("offer", "Al menos 1 producto activo", productCount > 0, "Carga tu primer producto para activar la vitrina."));
                break;
            case "B":
                var serviceCount = await _db.Services.CountAsync(x => x.PartnerId == partner.Id && x.IsActive);
                checklist.Add(BuildItem("offer", "Al menos 1 servicio activo", serviceCount > 0, "Crea un servicio para empezar a recibir reservas."));
                break;
            case "C":
                var qualifiedProfessionalCount = await _db.Professionals.CountAsync(x =>
                    x.TenantId == partner.TenantId &&
                    x.IsActive &&
                    x.IsVerified &&
                    !string.IsNullOrWhiteSpace(x.Name) &&
                    !string.IsNullOrWhiteSpace(x.Specialty) &&
                    (!string.IsNullOrWhiteSpace(x.Email) || !string.IsNullOrWhiteSpace(x.Phone)));
                checklist.Add(BuildItem(
                    "offer",
                    "Al menos 1 profesional verificado y completo",
                    qualifiedProfessionalCount > 0,
                    "Registra un profesional activo, verificado, con especialidad y contacto para publicar."));
                break;
        }

        var completed = checklist.Count(x => x.IsComplete);
        var percent = checklist.Count == 0 ? 100 : (int)Math.Round((double)completed * 100d / checklist.Count, MidpointRounding.AwayFromZero);
        var canPublish = checklist.All(x => x.IsComplete);
        var status = canPublish ? "ready" : completed == 0 ? "draft" : "incomplete";
        var statusLabel = canPublish ? "Listo para publicar" : completed == 0 ? "Borrador" : "Faltan pasos para publicar";
        var nextStep = BuildNextStep(normalizedType, checklist);

        return new PartnerActivationStatusResponse(
            partner.Id,
            normalizedType,
            partner.IsVisible,
            canPublish,
            percent,
            status,
            statusLabel,
            nextStep,
            checklist);
    }

    private static object ToPartnerResponse(Partner partner, PartnerActivationStatusResponse activation)
        => new
        {
            partner.Id,
            partner.TenantId,
            partner.Type,
            partner.Name,
            partner.Rut,
            partner.Address,
            partner.Phone,
            partner.Email,
            partner.IsVisible,
            partner.CreatedAt,
            partner.UpdatedAt,
            CategoryId = partner.Subcategory != null ? partner.Subcategory.CategoryId : (Guid?)null,
            CategoryName = partner.Subcategory?.Category?.Name,
            partner.SubcategoryId,
            SubcategoryName = partner.Subcategory?.Name,
            Activation = activation
        };

    private static PartnerChecklistItemResponse BuildItem(string key, string label, bool isComplete, string hint)
        => new(key, label, isComplete, isComplete ? null : hint);

    private static bool HasContactChannel(Partner partner)
        => !string.IsNullOrWhiteSpace(partner.Phone) || !string.IsNullOrWhiteSpace(partner.Email);

    private static string NormalizeType(string? type)
        => type?.Trim().ToUpperInvariant() switch
        {
            "B" => "B",
            "C" => "C",
            _ => "A"
        };

    private static string BuildNextStep(string type, IReadOnlyList<PartnerChecklistItemResponse> checklist)
    {
        var missing = checklist.FirstOrDefault(x => !x.IsComplete);
        if (missing is null)
        {
            return type switch
            {
                "A" => "Tu vitrina ya puede publicarse. Próximo paso: destacar productos y stock.",
                "B" => "Tu agenda ya puede publicarse. Próximo paso: definir horarios y disponibilidad.",
                "C" => "Tu perfil ya puede publicarse. Próximo paso: responder rápido a los primeros leads.",
                _ => "Tu negocio ya puede publicarse."
            };
        }

        return type switch
        {
            "A" => $"Siguiente paso: completa \"{missing.Label}\" para activar tu catálogo de productos.",
            "B" => $"Siguiente paso: completa \"{missing.Label}\" para activar tu oferta de servicios.",
            "C" => $"Siguiente paso: completa \"{missing.Label}\" para activar tu perfil profesional.",
            _ => $"Siguiente paso: completa \"{missing.Label}\"."
        };
    }

    private Guid? ResolveTenantIdFromUser()
    {
        return ResolveGuidClaim(User, AuthConstants.ClaimTenantId, "tenantId", "tenant_id");
    }

    private Guid? ResolvePartnerIdFromUser()
    {
        return ResolveGuidClaim(User, AuthConstants.ClaimPartnerId, "partnerId", "partner_id");
    }

    private static Guid? ResolveGuidClaim(ClaimsPrincipal? user, params string[] claimTypes)
    {
        if (user is null)
        {
            return null;
        }

        foreach (var claimType in claimTypes)
        {
            var raw = user.FindFirst(claimType)?.Value;
            if (Guid.TryParse(raw, out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }
}
