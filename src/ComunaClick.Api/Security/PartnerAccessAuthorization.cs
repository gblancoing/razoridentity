using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ComunaClick.Api.Persistence;
using ComunaClick.Common.Auth;
using Microsoft.EntityFrameworkCore;

namespace ComunaClick.Api.Security;

internal enum PartnerAccessResult
{
    Allowed,
    Forbidden,
    NotFound
}

internal static class PartnerAccessAuthorization
{
    public static async Task<PartnerAccessResult> EnsurePartnerAccessAsync(
        CoreDbContext db,
        ClaimsPrincipal user,
        Guid partnerId,
        Guid? tenantId,
        Guid? scopedPartnerId,
        CancellationToken cancellationToken = default)
    {
        var partner = await db.Partners.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == partnerId, cancellationToken);

        if (partner is null)
        {
            return PartnerAccessResult.NotFound;
        }

        if (tenantId.HasValue && partner.TenantId != tenantId.Value)
        {
            return PartnerAccessResult.Forbidden;
        }

        if (HasRole(user, "platform_admin"))
        {
            return PartnerAccessResult.Allowed;
        }

        if (scopedPartnerId.HasValue && scopedPartnerId.Value != partnerId)
        {
            return PartnerAccessResult.Forbidden;
        }

        if (scopedPartnerId.HasValue && scopedPartnerId.Value == partnerId)
        {
            return PartnerAccessResult.Allowed;
        }

        var userId = ResolveUserId(user);
        if (!userId.HasValue)
        {
            return PartnerAccessResult.Forbidden;
        }

        var hasStaff = await db.PartnerStaff.AsNoTracking()
            .AnyAsync(
                x => x.PartnerId == partnerId
                     && x.UserId == userId.Value
                     && x.TenantId == partner.TenantId,
                cancellationToken);

        return hasStaff ? PartnerAccessResult.Allowed : PartnerAccessResult.Forbidden;
    }

    /// <summary>
    /// Allows authenticated partner owners to preview catalog items before the business is published.
    /// </summary>
    public static async Task<bool> CanPreviewUnpublishedPartnerAsync(
        CoreDbContext db,
        ClaimsPrincipal user,
        Guid partnerId,
        CancellationToken cancellationToken = default)
    {
        if (user.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        if (HasRole(user, "platform_admin"))
        {
            return true;
        }

        var tokenPartnerId = ResolvePartnerId(user);
        if (tokenPartnerId == partnerId)
        {
            return true;
        }

        var userId = ResolveUserId(user);
        if (!userId.HasValue)
        {
            return false;
        }

        return await db.PartnerStaff.AsNoTracking()
            .AnyAsync(
                x => x.PartnerId == partnerId && x.UserId == userId.Value,
                cancellationToken);
    }

    private static Guid? ResolvePartnerId(ClaimsPrincipal user)
    {
        var raw = user.FindFirst(AuthConstants.ClaimPartnerId)?.Value;
        return Guid.TryParse(raw, out var parsed) ? parsed : null;
    }

    private static bool HasRole(ClaimsPrincipal user, string role)
    {
        var roleClaims = user.FindAll(AuthConstants.ClaimRole).Select(x => x.Value)
            .Concat(user.FindAll(AuthConstants.ClaimRoles).Select(x => x.Value));
        return roleClaims.Contains(role, StringComparer.OrdinalIgnoreCase);
    }

    private static Guid? ResolveUserId(ClaimsPrincipal user)
    {
        foreach (var claimType in new[]
                 {
                     JwtRegisteredClaimNames.Sub,
                     ClaimTypes.NameIdentifier,
                     "userId",
                     "user_id"
                 })
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
