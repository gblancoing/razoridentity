using System.Security.Claims;
using ComunaClick.Common.Auth;
using ComunaClick.Api.Persistence;

namespace ComunaClick.Api.Middleware;

public sealed class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string _tenantHeader;
    private readonly string _partnerHeader;

    public TenantResolutionMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        _tenantHeader = configuration["Tenant:HeaderName"] ?? "X-Tenant-Id";
        _partnerHeader = configuration["Tenant:PartnerHeaderName"] ?? "X-Partner-Id";
    }

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        Guid? tenantId = null;
        Guid? partnerId = null;

        var claimTenant = context.User?.FindFirst(AuthConstants.ClaimTenantId)?.Value;
        if (Guid.TryParse(claimTenant, out var tenantGuid))
        {
            tenantId = tenantGuid;
        }
        else if (context.Request.Headers.TryGetValue(_tenantHeader, out var headerValue) &&
                 Guid.TryParse(headerValue, out var headerGuid))
        {
            tenantId = headerGuid;
        }
        else if (context.Request.Headers.ContainsKey(_tenantHeader) && tenantId is null)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new { message = "Invalid X-Tenant-Id header." });
            return;
        }

        var claimPartner = context.User?.FindFirst(AuthConstants.ClaimPartnerId)?.Value;
        if (Guid.TryParse(claimPartner, out var partnerGuid))
        {
            partnerId = partnerGuid;
        }
        else if (context.Request.Headers.TryGetValue(_partnerHeader, out var partnerHeader) &&
                 Guid.TryParse(partnerHeader, out var partnerHeaderGuid))
        {
            partnerId = partnerHeaderGuid;
        }
        else if (context.Request.Headers.ContainsKey(_partnerHeader) && partnerId is null)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new { message = "Invalid X-Partner-Id header." });
            return;
        }

        tenantContext.Set(tenantId, partnerId);
        await _next(context);
    }
}
