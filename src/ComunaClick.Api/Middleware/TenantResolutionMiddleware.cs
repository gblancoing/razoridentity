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

        var claimTenant = ResolveGuidClaim(context.User, AuthConstants.ClaimTenantId, "tenantId", "tenant_id");
        if (claimTenant.HasValue)
        {
            tenantId = claimTenant.Value;
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

        var claimPartner = ResolveGuidClaim(context.User, AuthConstants.ClaimPartnerId, "partnerId", "partner_id");
        if (claimPartner.HasValue)
        {
            partnerId = claimPartner.Value;
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
