using ComunaClick.Common.Funnel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ComunaClick.Api.Modules.Funnel;

[ApiController]
[Route("v1/funnel/events")]
[EnableRateLimiting("public-write")]
public sealed class FunnelEventsController : ControllerBase
{
    private readonly ILogger<FunnelEventsController> _logger;

    public FunnelEventsController(ILogger<FunnelEventsController> logger)
    {
        _logger = logger;
    }

    [HttpPost]
    public ActionResult<FunnelEventAck> Track(FunnelEventRequest request)
    {
        _logger.LogInformation(
            "FunnelEvent {EventName} vertical={Vertical} entityType={EntityType} entityId={EntityId} tenantId={TenantId} origin={Origin} deviceType={DeviceType} ctaType={CtaType} step={Step} status={Status} timestamp={Timestamp} metadata={Metadata}",
            request.EventName,
            request.Vertical,
            request.EntityType,
            request.EntityId,
            request.TenantId,
            request.Origin,
            request.DeviceType,
            request.CtaType,
            request.Step,
            request.Status,
            request.Timestamp,
            request.Metadata);

        return Accepted(new FunnelEventAck(true));
    }
}
