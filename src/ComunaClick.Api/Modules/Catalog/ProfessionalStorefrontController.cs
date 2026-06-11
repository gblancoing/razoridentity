using ComunaClick.Api.Modules.Onboarding;
using ComunaClick.Api.Modules.Onboarding.Contracts;
using ComunaClick.Api.Persistence;
using ComunaClick.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ComunaClick.Api.Modules.Catalog;

[ApiController]
[Authorize(Policy = "partner.staff")]
[Route("v1/professionals/{professionalId:guid}/storefront")]
public sealed class ProfessionalStorefrontController : ControllerBase
{
  private readonly CoreDbContext _db;
  private readonly ITenantContext _tenantContext;
  private readonly StorefrontBannerStorage _bannerStorage;

  public ProfessionalStorefrontController(
    CoreDbContext db,
    ITenantContext tenantContext,
    StorefrontBannerStorage bannerStorage)
  {
    _db = db;
    _tenantContext = tenantContext;
    _bannerStorage = bannerStorage;
  }

  [HttpGet]
  public async Task<ActionResult<ProfessionalStorefrontResponse>> Get(Guid professionalId)
  {
    var professional = await _db.Professionals.AsNoTracking()
      .FirstOrDefaultAsync(x => x.Id == professionalId);
    if (professional is null)
    {
      return NotFound();
    }

    if (!await CanManageAsync(professional))
    {
      return Forbid();
    }

    return Ok(ToResponse(professional));
  }

  [HttpPatch]
  public async Task<ActionResult<ProfessionalStorefrontResponse>> Update(
    Guid professionalId,
    ProfessionalStorefrontUpdateRequest request)
  {
    var professional = await _db.Professionals.FirstOrDefaultAsync(x => x.Id == professionalId);
    if (professional is null)
    {
      return NotFound();
    }

    if (!await CanManageAsync(professional))
    {
      return Forbid();
    }

    if (request.RemoveBanner == true && !string.IsNullOrWhiteSpace(professional.BannerUrl))
    {
      _bannerStorage.DeleteProfessionalBanner(professional.TenantId, professional.Id, professional.BannerUrl);
      professional.BannerUrl = null;
    }

    if (request.ProfileHeadline is not null)
    {
      professional.ProfileHeadline = Normalize(request.ProfileHeadline);
    }

    if (request.Bio is not null)
    {
      professional.Bio = Normalize(request.Bio);
    }

    await _db.SaveChangesAsync();
    return Ok(ToResponse(professional));
  }

  [HttpPost("banner")]
  [RequestFormLimits(MultipartBodyLengthLimit = 6_000_000)]
  [RequestSizeLimit(6_000_000)]
  public async Task<ActionResult<ProfessionalStorefrontResponse>> UploadBanner(
    Guid professionalId,
    IFormFile file)
  {
    var professional = await _db.Professionals.FirstOrDefaultAsync(x => x.Id == professionalId);
    if (professional is null)
    {
      return NotFound();
    }

    if (!await CanManageAsync(professional))
    {
      return Forbid();
    }

    var url = await _bannerStorage.SaveProfessionalBannerAsync(
      professional.TenantId,
      professional.Id,
      file,
      HttpContext.RequestAborted);

    if (url is null)
    {
      return BadRequest(new { message = "No se pudo guardar el banner. Usá JPG, PNG o WebP (máx. 5 MB)." });
    }

    if (!string.IsNullOrWhiteSpace(professional.BannerUrl))
    {
      _bannerStorage.DeleteProfessionalBanner(professional.TenantId, professional.Id, professional.BannerUrl);
    }

    professional.BannerUrl = url;
    await _db.SaveChangesAsync();
    return Ok(ToResponse(professional));
  }

  private async Task<bool> CanManageAsync(ComunaClick.Api.Persistence.Entities.Professional professional)
  {
    if (!professional.PartnerId.HasValue)
    {
      return _tenantContext.TenantId == professional.TenantId;
    }

    var access = await PartnerAccessAuthorization.EnsurePartnerAccessAsync(
      _db,
      User,
      professional.PartnerId.Value,
      _tenantContext.TenantId,
      _tenantContext.PartnerId,
      HttpContext.RequestAborted);

    return access == PartnerAccessResult.Allowed;
  }

  private static ProfessionalStorefrontResponse ToResponse(ComunaClick.Api.Persistence.Entities.Professional professional)
    => new(
      professional.Id,
      professional.Name,
      professional.Specialty,
      professional.BannerUrl,
      professional.ProfileHeadline,
      professional.Bio,
      $"/buyer/detail/professional/{professional.Id}");

  private static string? Normalize(string? value)
    => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
