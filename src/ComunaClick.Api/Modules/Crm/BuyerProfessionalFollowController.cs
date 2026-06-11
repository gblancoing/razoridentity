using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ComunaClick.Api.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ComunaClick.Api.Modules.Crm;

[ApiController]
[Authorize(Policy = "buyer.profile")]
[Route("v1/buyer/professional-profile/follows")]
public sealed class BuyerProfessionalFollowController : ControllerBase
{
    private readonly CoreDbContext _db;
    private readonly ITenantContext _tenantContext;

    public BuyerProfessionalFollowController(CoreDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<FollowItemResponse>>> List(
        [FromQuery] Guid? tenantId,
        CancellationToken cancellationToken)
    {
        var tid = ResolveTenantId(tenantId);
        if (!tid.HasValue) return BadRequest(new { message = "TenantId is required." });

        var email = ResolveEmail(User);
        if (string.IsNullOrWhiteSpace(email)) return BadRequest(new { message = "Authenticated email claim is required." });

        var professional = await FindProfessionalByEmailAsync(tid.Value, email, cancellationToken);
        if (professional is null) return Ok(Array.Empty<FollowItemResponse>());

        var follows = await _db.ProfessionalFollows.AsNoTracking()
            .Where(x => x.FollowerProfessionalId == professional.Id)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        var professionalIds = follows
            .Where(x => x.FollowedType == "professional")
            .Select(x => x.FollowedId).ToList();
        var partnerIds = follows
            .Where(x => x.FollowedType == "partner")
            .Select(x => x.FollowedId).ToList();

        var profData = professionalIds.Count > 0
            ? await _db.Professionals.AsNoTracking()
                .Where(x => professionalIds.Contains(x.Id))
                .Select(x => new ProfData(x.Id, x.Name, x.Specialty, x.ProfilePhotoUrl))
                .ToListAsync(cancellationToken)
            : new List<ProfData>();
        var profById = profData.ToDictionary(x => x.Id);

        var partnerData = partnerIds.Count > 0
            ? await _db.Partners.AsNoTracking()
                .Where(x => partnerIds.Contains(x.Id))
                .Select(x => new PartnerData(x.Id, x.Name, x.LogoUrl))
                .ToListAsync(cancellationToken)
            : new List<PartnerData>();
        var partnerById = partnerData.ToDictionary(x => x.Id);

        var result = follows.Select(f =>
        {
            string? name = null;
            string? subtitle = null;
            string? photoUrl = null;

            if (f.FollowedType == "professional" && profById.TryGetValue(f.FollowedId, out var p))
            {
                name = p.Name;
                subtitle = p.Specialty;
                photoUrl = p.ProfilePhotoUrl;
            }
            else if (f.FollowedType == "partner" && partnerById.TryGetValue(f.FollowedId, out var pt))
            {
                name = pt.Name;
                photoUrl = pt.LogoUrl;
            }

            return new FollowItemResponse(f.Id, f.FollowedType, f.FollowedId, name, subtitle, photoUrl, f.CreatedAt);
        }).ToList();

        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Follow(
        [FromBody] FollowRequest request,
        [FromQuery] Guid? tenantId,
        CancellationToken cancellationToken)
    {
        var tid = ResolveTenantId(tenantId);
        if (!tid.HasValue) return BadRequest(new { message = "TenantId is required." });

        var email = ResolveEmail(User);
        if (string.IsNullOrWhiteSpace(email)) return BadRequest(new { message = "Authenticated email claim is required." });

        if (request.FollowedType != "professional" && request.FollowedType != "partner")
            return BadRequest(new { message = "followedType must be 'professional' or 'partner'." });

        var professional = await FindProfessionalByEmailAsync(tid.Value, email, cancellationToken);
        if (professional is null) return BadRequest(new { message = "You must have an active professional profile to follow." });

        if (request.FollowedType == "professional" && professional.Id == request.FollowedId)
            return BadRequest(new { message = "You cannot follow yourself." });

        var exists = await _db.ProfessionalFollows.AnyAsync(x =>
            x.FollowerProfessionalId == professional.Id
            && x.FollowedType == request.FollowedType
            && x.FollowedId == request.FollowedId, cancellationToken);

        if (!exists)
        {
            _db.ProfessionalFollows.Add(new()
            {
                FollowerProfessionalId = professional.Id,
                FollowedType = request.FollowedType,
                FollowedId = request.FollowedId,
                CreatedAt = DateTimeOffset.UtcNow
            });
            await _db.SaveChangesAsync(cancellationToken);
        }

        return Ok(new { isFollowing = true });
    }

    [HttpDelete("{followedType}/{followedId:guid}")]
    public async Task<IActionResult> Unfollow(
        string followedType,
        Guid followedId,
        [FromQuery] Guid? tenantId,
        CancellationToken cancellationToken)
    {
        var tid = ResolveTenantId(tenantId);
        if (!tid.HasValue) return BadRequest(new { message = "TenantId is required." });

        var email = ResolveEmail(User);
        if (string.IsNullOrWhiteSpace(email)) return BadRequest(new { message = "Authenticated email claim is required." });

        var professional = await FindProfessionalByEmailAsync(tid.Value, email, cancellationToken);
        if (professional is null) return Ok(new { isFollowing = false });

        var follow = await _db.ProfessionalFollows.FirstOrDefaultAsync(x =>
            x.FollowerProfessionalId == professional.Id
            && x.FollowedType == followedType
            && x.FollowedId == followedId, cancellationToken);

        if (follow is not null)
        {
            _db.ProfessionalFollows.Remove(follow);
            await _db.SaveChangesAsync(cancellationToken);
        }

        return Ok(new { isFollowing = false });
    }

    private Guid? ResolveTenantId(Guid? fromRequest)
    {
        var tid = fromRequest ?? _tenantContext.TenantId;
        return tid is { } g && g != Guid.Empty ? g : null;
    }

    private async Task<Persistence.Entities.Professional?> FindProfessionalByEmailAsync(Guid tenantId, string email, CancellationToken cancellationToken)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        return await _db.Professionals.AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.IsActive && x.Email != null && x.Email.ToLower() == normalizedEmail, cancellationToken);
    }

    private static string? ResolveEmail(ClaimsPrincipal user)
        => user.FindFirst(ClaimTypes.Email)?.Value
           ?? user.FindFirst(JwtRegisteredClaimNames.Email)?.Value
           ?? user.FindFirst("email")?.Value;
}

public sealed record FollowRequest(string FollowedType, Guid FollowedId);
public sealed record FollowItemResponse(Guid FollowId, string FollowedType, Guid FollowedId, string? Name, string? Subtitle, string? PhotoUrl, DateTimeOffset FollowedAt);
file sealed record ProfData(Guid Id, string? Name, string? Specialty, string? ProfilePhotoUrl);
file sealed record PartnerData(Guid Id, string? Name, string? LogoUrl);
