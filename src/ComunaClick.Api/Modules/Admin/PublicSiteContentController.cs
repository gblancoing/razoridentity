using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ComunaClick.Api.Modules.Admin;

[ApiController]
[Route("v1/public/site-content")]
[AllowAnonymous]
[EnableRateLimiting("public-read")]
public sealed class PublicSiteContentController : ControllerBase
{
    private readonly SiteContentService _siteContent;

    public PublicSiteContentController(SiteContentService siteContent)
    {
        _siteContent = siteContent;
    }

    [HttpGet]
    public async Task<ActionResult<SiteContentDto>> Get(CancellationToken cancellationToken)
        => Ok(await _siteContent.GetAsync(cancellationToken));
}
