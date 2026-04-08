using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ComunaClick.Api.Modules.Admin;

[ApiController]
[Route("v1/admin/site-content")]
[Authorize(Policy = "platform.admin")]
public sealed class AdminContentController : ControllerBase
{
    private readonly SiteContentService _siteContent;

    public AdminContentController(SiteContentService siteContent)
    {
        _siteContent = siteContent;
    }

    [HttpGet]
    public async Task<ActionResult<SiteContentDto>> Get(CancellationToken cancellationToken)
        => Ok(await _siteContent.GetAsync(cancellationToken));

    [HttpPut]
    public async Task<ActionResult<SiteContentDto>> Update(SiteContentUpdateRequest request, CancellationToken cancellationToken)
        => Ok(await _siteContent.SaveAsync(request, cancellationToken));
}
