using ComunaClick.Api.Modules.Onboarding.Contracts;
using ComunaClick.Api.Persistence;
using ComunaClick.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ComunaClick.Api.Modules.Onboarding;

[ApiController]
[Authorize(Policy = "partner.staff")]
[Route("v1/partners/{partnerId:guid}/storefront")]
public sealed class PartnerStorefrontController : ControllerBase
{
  private readonly CoreDbContext _db;
  private readonly ITenantContext _tenantContext;
  private readonly StorefrontBannerStorage _bannerStorage;

  public PartnerStorefrontController(
    CoreDbContext db,
    ITenantContext tenantContext,
    StorefrontBannerStorage bannerStorage)
  {
    _db = db;
    _tenantContext = tenantContext;
    _bannerStorage = bannerStorage;
  }

  [HttpGet]
  public async Task<ActionResult<PartnerStorefrontResponse>> Get(Guid partnerId)
  {
    var partner = await LoadPartnerAsync(partnerId);
    if (partner is null)
    {
      return NotFound();
    }

    if (!await CanAccessAsync(partnerId))
    {
      return Forbid();
    }

    return Ok(ToResponse(partner));
  }

  [HttpPatch]
  public async Task<ActionResult<PartnerStorefrontResponse>> Update(
    Guid partnerId,
    PartnerStorefrontUpdateRequest request)
  {
    var partner = await _db.Partners.FirstOrDefaultAsync(x => x.Id == partnerId);
    if (partner is null)
    {
      return NotFound();
    }

    if (!await CanAccessAsync(partnerId))
    {
      return Forbid();
    }

    if (request.RemoveBanner == true && !string.IsNullOrWhiteSpace(partner.BannerUrl))
    {
      _bannerStorage.DeletePartnerBanner(partner.TenantId, partner.Id, partner.BannerUrl);
      partner.BannerUrl = null;
    }

    if (request.RemoveLogo == true && !string.IsNullOrWhiteSpace(partner.LogoUrl))
    {
      _bannerStorage.DeletePartnerLogo(partner.TenantId, partner.Id, partner.LogoUrl);
      partner.LogoUrl = null;
    }

    if (request.StorefrontTagline is not null)
    {
      partner.StorefrontTagline = Normalize(request.StorefrontTagline);
    }

    if (request.StorefrontAbout is not null)
    {
      partner.StorefrontAbout = Normalize(request.StorefrontAbout);
    }

    if (request.StorefrontHighlight1 is not null)
    {
      partner.StorefrontHighlight1 = Normalize(request.StorefrontHighlight1);
    }

    if (request.StorefrontHighlight2 is not null)
    {
      partner.StorefrontHighlight2 = Normalize(request.StorefrontHighlight2);
    }

    if (request.StorefrontHighlight3 is not null)
    {
      partner.StorefrontHighlight3 = Normalize(request.StorefrontHighlight3);
    }

    partner.UpdatedAt = DateTimeOffset.UtcNow;
    await _db.SaveChangesAsync();
    return Ok(ToResponse(partner));
  }

  [HttpPost("banner")]
  [RequestFormLimits(MultipartBodyLengthLimit = 6_000_000)]
  [RequestSizeLimit(6_000_000)]
  public async Task<ActionResult<PartnerStorefrontResponse>> UploadBanner(
    Guid partnerId,
    IFormFile file)
  {
    var partner = await _db.Partners.FirstOrDefaultAsync(x => x.Id == partnerId);
    if (partner is null)
    {
      return NotFound();
    }

    if (!await CanAccessAsync(partnerId))
    {
      return Forbid();
    }

    var url = await _bannerStorage.SavePartnerBannerAsync(
      partner.TenantId,
      partner.Id,
      file,
      HttpContext.RequestAborted);

    if (url is null)
    {
      return BadRequest(new { message = "No se pudo guardar el banner. Usá JPG, PNG o WebP (máx. 5 MB)." });
    }

    if (!string.IsNullOrWhiteSpace(partner.BannerUrl))
    {
      _bannerStorage.DeletePartnerBanner(partner.TenantId, partner.Id, partner.BannerUrl);
    }

    partner.BannerUrl = url;
    partner.UpdatedAt = DateTimeOffset.UtcNow;
    await _db.SaveChangesAsync();
    return Ok(ToResponse(partner));
  }

  [HttpPost("logo")]
  [RequestFormLimits(MultipartBodyLengthLimit = 5_000_000)]
  [RequestSizeLimit(5_000_000)]
  public async Task<ActionResult<PartnerStorefrontResponse>> UploadLogo(
    Guid partnerId,
    IFormFile file)
  {
    var partner = await _db.Partners.FirstOrDefaultAsync(x => x.Id == partnerId);
    if (partner is null)
    {
      return NotFound();
    }

    if (!await CanAccessAsync(partnerId))
    {
      return Forbid();
    }

    var url = await _bannerStorage.SavePartnerLogoAsync(
      partner.TenantId,
      partner.Id,
      file,
      HttpContext.RequestAborted);

    if (url is null)
    {
      return BadRequest(new { message = "No se pudo guardar el logo. Usá JPG, PNG, WebP o GIF (máx. 4 MB)." });
    }

    if (!string.IsNullOrWhiteSpace(partner.LogoUrl))
    {
      _bannerStorage.DeletePartnerLogo(partner.TenantId, partner.Id, partner.LogoUrl);
    }

    partner.LogoUrl = url;
    partner.UpdatedAt = DateTimeOffset.UtcNow;
    await _db.SaveChangesAsync();
    return Ok(ToResponse(partner));
  }

  private async Task<ComunaClick.Api.Persistence.Entities.Partner?> LoadPartnerAsync(Guid partnerId)
    => await _db.Partners.AsNoTracking().FirstOrDefaultAsync(x => x.Id == partnerId);

  private async Task<bool> CanAccessAsync(Guid partnerId)
  {
    var access = await PartnerAccessAuthorization.EnsurePartnerAccessAsync(
      _db,
      User,
      partnerId,
      _tenantContext.TenantId,
      _tenantContext.PartnerId,
      HttpContext.RequestAborted);

    return access == PartnerAccessResult.Allowed;
  }

  private static PartnerStorefrontResponse ToResponse(ComunaClick.Api.Persistence.Entities.Partner partner)
    => new(
      partner.Id,
      partner.BannerUrl,
      partner.LogoUrl,
      partner.StorefrontTagline,
      partner.StorefrontAbout,
      partner.StorefrontHighlight1,
      partner.StorefrontHighlight2,
      partner.StorefrontHighlight3,
      $"/buyer/detail/partner/{partner.Id}");

  private static string? Normalize(string? value)
    => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
