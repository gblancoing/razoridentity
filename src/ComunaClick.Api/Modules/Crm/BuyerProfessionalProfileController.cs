using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using ComunaClick.Api.Geo;
using ComunaClick.Api.Modules.Crm.Contracts;
using ComunaClick.Api.Modules.Onboarding;
using ComunaClick.Api.Modules.Onboarding.Contracts;
using ComunaClick.Api.Persistence;
using ComunaClick.Api.Persistence.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ComunaClick.Api.Modules.Crm;

[ApiController]
[Authorize(Policy = "buyer.profile")]
[Route("v1/buyer/professional-profile")]
public sealed class BuyerProfessionalProfileController : ControllerBase
{
    private readonly CoreDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly StorefrontBannerStorage _bannerStorage;

    public BuyerProfessionalProfileController(CoreDbContext db, ITenantContext tenantContext, StorefrontBannerStorage bannerStorage)
    {
        _db = db;
        _tenantContext = tenantContext;
        _bannerStorage = bannerStorage;
    }

    [HttpGet]
    public async Task<ActionResult<BuyerProfessionalProfileResponse>> Get([FromQuery] Guid? tenantId, CancellationToken cancellationToken)
    {
        var tid = ResolveTenantId(tenantId);
        if (!tid.HasValue)
        {
            return BadRequest(new { message = "TenantId is required." });
        }

        var email = ResolveEmail(User);
        if (string.IsNullOrWhiteSpace(email))
        {
            return BadRequest(new { message = "Authenticated email claim is required." });
        }

        var professional = await FindByEmailAsync(tid.Value, email, cancellationToken);
        return Ok(Map(professional));
    }

    [HttpPut]
    public async Task<ActionResult<BuyerProfessionalProfileResponse>> Upsert(
        [FromBody] BuyerProfessionalProfileUpsertRequest request,
        CancellationToken cancellationToken)
    {
        var tid = ResolveTenantId(request.TenantId);
        if (!tid.HasValue)
        {
            return BadRequest(new { message = "TenantId is required." });
        }

        var email = ResolveEmail(User);
        if (string.IsNullOrWhiteSpace(email))
        {
            return BadRequest(new { message = "Authenticated email claim is required." });
        }

        var linksRequest = new ProfileWebLinksUpdateRequest(
            request.WebsiteUrl,
            request.InstagramUrl,
            request.FacebookUrl,
            request.LinkedInUrl,
            request.XUrl,
            request.TikTokUrl,
            request.YouTubeUrl,
            request.OtherLinkLabel,
            request.OtherLinkUrl);

        var linksError = ProfileWebLinksNormalizer.Validate(linksRequest);
        if (linksError is not null)
        {
            return BadRequest(new { message = linksError });
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var activate = request.Activate ?? true;

        var professional = await _db.Professionals
            .FirstOrDefaultAsync(x => x.TenantId == tid.Value && x.Email != null && x.Email.ToLower() == normalizedEmail, cancellationToken);

        if (!activate)
        {
            if (professional is null)
            {
                return Ok(Map(null));
            }

            professional.IsActive = false;
            await _db.SaveChangesAsync(cancellationToken);
            return Ok(Map(professional));
        }

        var displayName = string.IsNullOrWhiteSpace(request.Name)
            ? ResolveName(User) ?? normalizedEmail
            : request.Name.Trim();

        if (professional is null)
        {
            var geo = await GeoContextResolver.ResolveFromTenantAsync(_db, tid.Value);
            professional = new Professional
            {
                TenantId = tid.Value,
                CountryId = geo.CountryId,
                RegionId = geo.RegionId,
                ComunaId = geo.ComunaId,
                Name = displayName,
                Email = normalizedEmail,
                Phone = NormalizeOptional(request.Phone),
                Specialty = NormalizeOptional(request.Specialty),
                Bio = NormalizeOptional(request.Bio),
                IsVerified = false,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            };
            _db.Professionals.Add(professional);
        }
        else
        {
            professional.Name = displayName;
            professional.Phone = request.Phone is null ? professional.Phone : NormalizeOptional(request.Phone);
            professional.Specialty = request.Specialty is null ? professional.Specialty : NormalizeOptional(request.Specialty);
            professional.Bio = request.Bio is null ? professional.Bio : NormalizeOptional(request.Bio);
            professional.IsActive = true;
        }

        ProfileWebLinksNormalizer.ApplyToProfessional(professional, linksRequest);

        await _db.SaveChangesAsync(cancellationToken);
        return Ok(Map(professional));
    }

    [HttpPost("banner")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<BuyerProfessionalProfileResponse>> UploadBanner(
        IFormFile file,
        [FromQuery] Guid? tenantId,
        CancellationToken cancellationToken)
    {
        var tid = ResolveTenantId(tenantId);
        if (!tid.HasValue)
            return BadRequest(new { message = "TenantId is required." });

        var email = ResolveEmail(User);
        if (string.IsNullOrWhiteSpace(email))
            return BadRequest(new { message = "Authenticated email claim is required." });

        var professional = await _db.Professionals
            .FirstOrDefaultAsync(x => x.TenantId == tid.Value && x.Email != null && x.Email.ToLower() == email.Trim().ToLowerInvariant(), cancellationToken);

        if (professional is null)
            return NotFound(new { message = "Professional profile not found." });

        var url = await _bannerStorage.SaveProfessionalBannerAsync(tid.Value, professional.Id, file, cancellationToken);
        if (url is null)
            return BadRequest(new { message = "Invalid file. Use JPG, PNG or WebP, max 5 MB." });

        professional.BannerUrl = url;
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(Map(professional));
    }

    [HttpPost("photo")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<BuyerProfessionalProfileResponse>> UploadPhoto(
        IFormFile file,
        [FromQuery] Guid? tenantId,
        CancellationToken cancellationToken)
    {
        var tid = ResolveTenantId(tenantId);
        if (!tid.HasValue)
            return BadRequest(new { message = "TenantId is required." });

        var email = ResolveEmail(User);
        if (string.IsNullOrWhiteSpace(email))
            return BadRequest(new { message = "Authenticated email claim is required." });

        var professional = await _db.Professionals
            .FirstOrDefaultAsync(x => x.TenantId == tid.Value && x.Email != null && x.Email.ToLower() == email.Trim().ToLowerInvariant(), cancellationToken);

        if (professional is null)
            return NotFound(new { message = "Professional profile not found." });

        if (!string.IsNullOrWhiteSpace(professional.ProfilePhotoUrl))
            _bannerStorage.DeleteProfessionalPhoto(tid.Value, professional.Id, professional.ProfilePhotoUrl);

        var url = await _bannerStorage.SaveProfessionalPhotoAsync(tid.Value, professional.Id, file, cancellationToken);
        if (url is null)
            return BadRequest(new { message = "Invalid file. Use JPG, PNG, WebP or GIF, max 4 MB." });

        professional.ProfilePhotoUrl = url;
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(Map(professional));
    }

    [HttpDelete("photo")]
    public async Task<ActionResult<BuyerProfessionalProfileResponse>> RemovePhoto(
        [FromQuery] Guid? tenantId,
        CancellationToken cancellationToken)
    {
        var tid = ResolveTenantId(tenantId);
        if (!tid.HasValue)
            return BadRequest(new { message = "TenantId is required." });

        var email = ResolveEmail(User);
        if (string.IsNullOrWhiteSpace(email))
            return BadRequest(new { message = "Authenticated email claim is required." });

        var professional = await _db.Professionals
            .FirstOrDefaultAsync(x => x.TenantId == tid.Value && x.Email != null && x.Email.ToLower() == email.Trim().ToLowerInvariant(), cancellationToken);

        if (professional is null)
            return NotFound(new { message = "Professional profile not found." });

        if (!string.IsNullOrWhiteSpace(professional.ProfilePhotoUrl))
        {
            _bannerStorage.DeleteProfessionalPhoto(tid.Value, professional.Id, professional.ProfilePhotoUrl);
            professional.ProfilePhotoUrl = null;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return Ok(Map(professional));
    }

    [HttpDelete("banner")]
    public async Task<ActionResult<BuyerProfessionalProfileResponse>> RemoveBanner(
        [FromQuery] Guid? tenantId,
        CancellationToken cancellationToken)
    {
        var tid = ResolveTenantId(tenantId);
        if (!tid.HasValue)
            return BadRequest(new { message = "TenantId is required." });

        var email = ResolveEmail(User);
        if (string.IsNullOrWhiteSpace(email))
            return BadRequest(new { message = "Authenticated email claim is required." });

        var professional = await _db.Professionals
            .FirstOrDefaultAsync(x => x.TenantId == tid.Value && x.Email != null && x.Email.ToLower() == email.Trim().ToLowerInvariant(), cancellationToken);

        if (professional is null)
            return NotFound(new { message = "Professional profile not found." });

        if (!string.IsNullOrWhiteSpace(professional.BannerUrl))
        {
            _bannerStorage.DeleteProfessionalBanner(tid.Value, professional.Id, professional.BannerUrl);
            professional.BannerUrl = null;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return Ok(Map(professional));
    }

    [HttpPost("certifications")]
    public async Task<ActionResult<BuyerProfessionalProfileResponse>> AddCertification(
        [FromBody] AddCertificationRequest request,
        [FromQuery] Guid? tenantId,
        CancellationToken cancellationToken)
    {
        var tid = ResolveTenantId(tenantId);
        if (!tid.HasValue) return BadRequest(new { message = "TenantId is required." });

        var email = ResolveEmail(User);
        if (string.IsNullOrWhiteSpace(email)) return BadRequest(new { message = "Authenticated email claim is required." });

        var professional = await _db.Professionals
            .FirstOrDefaultAsync(x => x.TenantId == tid.Value && x.Email != null && x.Email.ToLower() == email.Trim().ToLowerInvariant(), cancellationToken);
        if (professional is null) return NotFound(new { message = "Professional profile not found." });

        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "Name is required." });

        var certs = DeserializeCerts(professional.CertificationsJson);
        certs.Add(new CertificationItem(
            Guid.NewGuid().ToString("N"),
            request.Name.Trim(),
            NormalizeOptional(request.Institution),
            request.Year,
            NormalizeOptional(request.Url)));

        professional.CertificationsJson = JsonSerializer.Serialize(certs);
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(Map(professional));
    }

    [HttpDelete("certifications/{certId}")]
    public async Task<ActionResult<BuyerProfessionalProfileResponse>> RemoveCertification(
        string certId,
        [FromQuery] Guid? tenantId,
        CancellationToken cancellationToken)
    {
        var tid = ResolveTenantId(tenantId);
        if (!tid.HasValue) return BadRequest(new { message = "TenantId is required." });

        var email = ResolveEmail(User);
        if (string.IsNullOrWhiteSpace(email)) return BadRequest(new { message = "Authenticated email claim is required." });

        var professional = await _db.Professionals
            .FirstOrDefaultAsync(x => x.TenantId == tid.Value && x.Email != null && x.Email.ToLower() == email.Trim().ToLowerInvariant(), cancellationToken);
        if (professional is null) return NotFound(new { message = "Professional profile not found." });

        var certs = DeserializeCerts(professional.CertificationsJson);
        certs.RemoveAll(c => c.Id == certId);
        professional.CertificationsJson = certs.Count == 0 ? null : JsonSerializer.Serialize(certs);
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(Map(professional));
    }

    private static List<CertificationItem> DeserializeCerts(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<List<CertificationItem>>(json) ?? []; }
        catch { return []; }
    }

    private sealed record CertificationItem(string Id, string Name, string? Institution, int? Year, string? Url);

    private Guid? ResolveTenantId(Guid? fromRequest)
    {
        var tid = fromRequest ?? _tenantContext.TenantId;
        return tid is { } g && g != Guid.Empty ? g : null;
    }

    private async Task<Professional?> FindByEmailAsync(Guid tenantId, string email, CancellationToken cancellationToken)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        return await _db.Professionals.AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Email != null && x.Email.ToLower() == normalizedEmail, cancellationToken);
    }

    private static BuyerProfessionalProfileResponse Map(Professional? professional)
    {
        if (professional is null)
        {
            return new BuyerProfessionalProfileResponse(
                HasProfile: false,
                ProfessionalId: null,
                Name: null,
                Email: null,
                Phone: null,
                Specialty: null,
                Bio: null,
                IsVerified: false,
                IsActive: false);
        }

        return new BuyerProfessionalProfileResponse(
            HasProfile: true,
            ProfessionalId: professional.Id,
            Name: professional.Name,
            Email: professional.Email,
            Phone: professional.Phone,
            Specialty: professional.Specialty,
            Bio: professional.Bio,
            IsVerified: professional.IsVerified,
            IsActive: professional.IsActive,
            WebsiteUrl: professional.WebsiteUrl,
            InstagramUrl: professional.InstagramUrl,
            FacebookUrl: professional.FacebookUrl,
            LinkedInUrl: professional.LinkedInUrl,
            XUrl: professional.XUrl,
            TikTokUrl: professional.TikTokUrl,
            YouTubeUrl: professional.YouTubeUrl,
            OtherLinkLabel: professional.OtherLinkLabel,
            OtherLinkUrl: professional.OtherLinkUrl,
            BannerUrl: professional.BannerUrl,
            ProfilePhotoUrl: professional.ProfilePhotoUrl,
            ProfileViewCount: professional.ProfileViewCount,
            CertificationsJson: professional.CertificationsJson);
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? ResolveEmail(ClaimsPrincipal user)
        => user.FindFirst(ClaimTypes.Email)?.Value
           ?? user.FindFirst(JwtRegisteredClaimNames.Email)?.Value
           ?? user.FindFirst("email")?.Value;

    private static string? ResolveName(ClaimsPrincipal user)
        => user.FindFirst(ClaimTypes.Name)?.Value
           ?? user.FindFirst("name")?.Value
           ?? user.FindFirst(JwtRegisteredClaimNames.Name)?.Value;
}
