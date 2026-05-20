using System.Security.Cryptography;
using ComunaClick.Acl.Contracts.Auth;
using ComunaClick.Acl.Domain;
using ComunaClick.Acl.Persistence;
using ComunaClick.Acl.Security;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ComunaClick.Acl.Controllers;

[ApiController]
[Route("v1/auth")]
[EnableRateLimiting("auth")]
public sealed class AuthController : ControllerBase
{
    private readonly AclDbContext _db;
    private readonly JwtTokenService _tokenService;
    private readonly IConfiguration _configuration;
    private readonly RecaptchaV3Verifier _recaptchaVerifier;
    private readonly IReadOnlyDictionary<string, IExternalTokenValidator> _externalValidators;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        AclDbContext db,
        JwtTokenService tokenService,
        IConfiguration configuration,
        RecaptchaV3Verifier recaptchaVerifier,
        IEnumerable<IExternalTokenValidator> validators,
        ILogger<AuthController> logger)
    {
        _db = db;
        _tokenService = tokenService;
        _configuration = configuration;
        _recaptchaVerifier = recaptchaVerifier;
        _externalValidators = validators.ToDictionary(v => v.Provider, StringComparer.OrdinalIgnoreCase);
        _logger = logger;
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        if (!await _recaptchaVerifier.VerifyAsync(request.RecaptchaToken, "login", HttpContext.Connection.RemoteIpAddress?.ToString(), HttpContext.RequestAborted))
        {
            return BadRequest(new { message = "No se pudo validar reCAPTCHA." });
        }

        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { message = "Email and password are required." });
        }

        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users
            .Include(x => x.UserRoles).ThenInclude(x => x.Role)
            .Include(x => x.TenantScopes)
            .Include(x => x.TenantAccess)
            .FirstOrDefaultAsync(x => x.Email.ToLower() == email);

        if (user is null || !user.IsActive)
        {
            return Unauthorized();
        }

        var allowLegacySha256 = _configuration.GetValue("PasswordHashing:AllowLegacySha256", true);
        var allowPlainText = _configuration.GetValue("PasswordHashing:AllowPlainText", false);
        if (!PasswordHasher.Verify(request.Password, user.PasswordHash, allowLegacySha256, allowPlainText))
        {
            return Unauthorized();
        }

        var roles = await ResolveRolesAsync(user, HttpContext.RequestAborted);
        if (!TryResolveScope(user, roles, request.TenantId, request.PartnerId, out var tenantId, out var partnerId, out var error))
        {
            return error!;
        }

        var now = DateTimeOffset.UtcNow;
        var accessToken = _tokenService.CreateAccessToken(user, roles, tenantId, partnerId, now);
        var refresh = await IssueRefreshToken(user, now);

        var accessMinutes = _configuration.GetValue("Jwt:AccessTokenMinutes", 30);
        var response = new AuthResponse(
            AccessToken: accessToken,
            RefreshToken: refresh.RawToken,
            ExpiresIn: accessMinutes * 60,
            TokenType: "Bearer",
            User: new AuthUserDto(user.Id, user.Email, user.DisplayName, roles, tenantId, partnerId));

        return Ok(response);
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        if (!await _recaptchaVerifier.VerifyAsync(request.RecaptchaToken, "register", HttpContext.Connection.RemoteIpAddress?.ToString(), HttpContext.RequestAborted))
        {
            return BadRequest(new { message = "No se pudo validar reCAPTCHA." });
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { message = "Name is required." });
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new { message = "Email is required." });
        }

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
        {
            return BadRequest(new { message = "Password must be at least 8 characters." });
        }

        var email = request.Email.Trim().ToLowerInvariant();
        var exists = await _db.Users.AnyAsync(x => x.Email.ToLower() == email);
        if (exists)
        {
            return Conflict(new { message = "Email already exists." });
        }

        var now = DateTimeOffset.UtcNow;
        var user = new User
        {
            Email = email,
            DisplayName = request.Name.Trim(),
            PasswordHash = PasswordHasher.Hash(request.Password),
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        var defaultRole = _configuration["Auth:RegisterDefaultRole"] ?? _configuration["Auth:ExternalDefaultRole"] ?? "customer";
        var role = await _db.Roles.FirstOrDefaultAsync(x => x.Name == defaultRole);
        if (role is not null)
        {
            user.UserRoles.Add(new UserRole { RoleId = role.Id, User = user });
        }

        Guid? tenantId = null;
        var defaultTenantRaw = _configuration["Auth:RegisterDefaultTenantId"];
        if (Guid.TryParse(defaultTenantRaw, out var defaultTenantId))
        {
            tenantId = defaultTenantId;
            user.TenantAccess.Add(new UserTenantAccess
            {
                User = user,
                TenantId = defaultTenantId
            });
        }

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var roles = user.UserRoles.Select(x => x.Role?.Name ?? defaultRole).ToList();
        var accessToken = _tokenService.CreateAccessToken(user, roles, tenantId, null, now);
        var refresh = await IssueRefreshToken(user, now);
        var accessMinutes = _configuration.GetValue("Jwt:AccessTokenMinutes", 30);

        var response = new AuthResponse(
            AccessToken: accessToken,
            RefreshToken: refresh.RawToken,
            ExpiresIn: accessMinutes * 60,
            TokenType: "Bearer",
            User: new AuthUserDto(user.Id, user.Email, user.DisplayName, roles, tenantId, null));

        return Ok(response);
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return BadRequest(new { message = "Refresh token is required." });
        }

        var pepper = _configuration["Jwt:RefreshTokenPepper"];
        var tokenHash = TokenHasher.Hash(request.RefreshToken.Trim(), pepper);
        var refreshToken = await _db.RefreshTokens
            .Include(x => x.User)
            .ThenInclude(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .Include(x => x.User)
            .ThenInclude(x => x.TenantScopes)
            .Include(x => x.User)
            .ThenInclude(x => x.TenantAccess)
            .FirstOrDefaultAsync(x => x.TokenHash == tokenHash);

        if (refreshToken is null || refreshToken.RevokedAt.HasValue)
        {
            return Unauthorized();
        }

        if (refreshToken.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            return Unauthorized();
        }

        var user = refreshToken.User;
        if (!user.IsActive)
        {
            return Unauthorized();
        }

        var roles = await ResolveRolesAsync(user, HttpContext.RequestAborted);
        if (!TryResolveScope(user, roles, request.TenantId, request.PartnerId, out var tenantId, out var partnerId, out var error))
        {
            return error!;
        }

        refreshToken.RevokedAt = DateTimeOffset.UtcNow;
        var now = DateTimeOffset.UtcNow;
        var newRefresh = await IssueRefreshToken(user, now);

        var accessToken = _tokenService.CreateAccessToken(user, roles, tenantId, partnerId, now);
        var accessMinutes = _configuration.GetValue("Jwt:AccessTokenMinutes", 30);

        await _db.SaveChangesAsync();

        var response = new AuthResponse(
            AccessToken: accessToken,
            RefreshToken: newRefresh.RawToken,
            ExpiresIn: accessMinutes * 60,
            TokenType: "Bearer",
            User: new AuthUserDto(user.Id, user.Email, user.DisplayName, roles, tenantId, partnerId));

        return Ok(response);
    }

    [HttpPost("google")]
    public async Task<ActionResult<AuthResponse>> Google(SocialLoginRequest request, CancellationToken cancellationToken)
    {
        return await ExternalLogin("google", request, cancellationToken);
    }

    [HttpPost("apple")]
    public async Task<ActionResult<AuthResponse>> Apple(SocialLoginRequest request, CancellationToken cancellationToken)
    {
        return await ExternalLogin("apple", request, cancellationToken);
    }

    [HttpPost("password-reset")]
    public async Task<ActionResult<PasswordResetStartResponse>> StartPasswordReset(PasswordResetStartRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new { message = "Email is required." });
        }

        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(x => x.Email.ToLower() == email);
        if (user is null || !user.IsActive)
        {
            return Accepted(new PasswordResetStartResponse(null));
        }

        var now = DateTimeOffset.UtcNow;
        var existingTokens = await _db.PasswordResetTokens
            .Where(x => x.UserId == user.Id && x.UsedAt == null && x.ExpiresAt > now)
            .ToListAsync();

        foreach (var token in existingTokens)
        {
            token.UsedAt = now;
        }

        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        var pepper = _configuration["PasswordReset:TokenPepper"] ?? _configuration["Jwt:RefreshTokenPepper"];
        var tokenHash = TokenHasher.Hash(rawToken, pepper);
        var minutes = _configuration.GetValue("PasswordReset:TokenMinutes", 30);

        var resetToken = new PasswordResetToken
        {
            UserId = user.Id,
            TokenHash = tokenHash,
            IssuedAt = now,
            ExpiresAt = now.AddMinutes(minutes),
            UserAgent = Request.Headers.UserAgent.ToString(),
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        };

        _db.PasswordResetTokens.Add(resetToken);
        await _db.SaveChangesAsync();

        var returnToken = _configuration.GetValue("PasswordReset:ReturnToken", false);
        return Accepted(new PasswordResetStartResponse(returnToken ? rawToken : null));
    }

    [HttpPost("password-reset/confirm")]
    public async Task<IActionResult> ConfirmPasswordReset(PasswordResetConfirmRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return BadRequest(new { message = "Token is required." });
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
        {
            return BadRequest(new { message = "Password must be at least 8 characters." });
        }

        var pepper = _configuration["PasswordReset:TokenPepper"] ?? _configuration["Jwt:RefreshTokenPepper"];
        var tokenHash = TokenHasher.Hash(request.Token.Trim(), pepper);
        var now = DateTimeOffset.UtcNow;

        var resetToken = await _db.PasswordResetTokens
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.TokenHash == tokenHash);

        if (resetToken is null || resetToken.UsedAt.HasValue || resetToken.ExpiresAt <= now)
        {
            return Unauthorized();
        }

        if (!resetToken.User.IsActive)
        {
            return Unauthorized();
        }

        resetToken.User.PasswordHash = PasswordHasher.Hash(request.NewPassword);
        resetToken.User.UpdatedAt = now;
        resetToken.UsedAt = now;

        await _db.SaveChangesAsync();
        return Ok();
    }

    private bool TryResolveScope(User user, IReadOnlyList<string> roles, Guid? requestedTenantId, Guid? requestedPartnerId,
        out Guid? tenantId, out Guid? partnerId, out ActionResult<AuthResponse>? error)
    {
        tenantId = null;
        partnerId = null;
        error = null;

        var scopes = user.TenantScopes.ToList();
        var tenantAccess = user.TenantAccess.ToList();
        var isSuperAdmin = user.IsSuperAdmin;

        if (requestedTenantId.HasValue)
        {
            var matches = scopes.Where(x => x.TenantId == requestedTenantId.Value).ToList();
            var hasTenantAccess = tenantAccess.Any(x => x.TenantId == requestedTenantId.Value);
            if (matches.Count == 0 && !hasTenantAccess && !roles.Contains("platform_admin", StringComparer.OrdinalIgnoreCase) && !isSuperAdmin)
            {
                error = Forbid();
                return false;
            }

            tenantId = requestedTenantId;

            if (requestedPartnerId.HasValue)
            {
                var partnerMatch = matches.FirstOrDefault(x => x.PartnerId == requestedPartnerId);
                var tenantLevelMatch = matches.Any(x => !x.PartnerId.HasValue || string.Equals(x.ScopeType, "tenant", StringComparison.OrdinalIgnoreCase));
                if (partnerMatch is null && !tenantLevelMatch && !hasTenantAccess)
                {
                    error = Forbid();
                    return false;
                }
                partnerId = requestedPartnerId;
            }
            else if (matches.Count == 1)
            {
                partnerId = matches[0].PartnerId;
            }

            return true;
        }

        if (scopes.Count == 1)
        {
            tenantId = scopes[0].TenantId;
            partnerId = scopes[0].PartnerId;
            return true;
        }

        if (scopes.Count > 1 && !roles.Contains("platform_admin", StringComparer.OrdinalIgnoreCase) && !isSuperAdmin)
        {
            error = BadRequest(new { message = "TenantId is required for users with multiple scopes." });
            return false;
        }

        return true;
    }

    private async Task<(RefreshToken Token, string RawToken)> IssueRefreshToken(User user, DateTimeOffset now)
    {
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var pepper = _configuration["Jwt:RefreshTokenPepper"];
        var tokenHash = TokenHasher.Hash(rawToken, pepper);
        var refreshDays = _configuration.GetValue("Jwt:RefreshTokenDays", 7);

        var refreshToken = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = tokenHash,
            IssuedAt = now,
            ExpiresAt = now.AddDays(refreshDays),
            UserAgent = Request.Headers.UserAgent.ToString(),
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        };

        _db.RefreshTokens.Add(refreshToken);
        await _db.SaveChangesAsync();

        return (refreshToken, rawToken);
    }

    private async Task<ActionResult<AuthResponse>> ExternalLogin(string provider, SocialLoginRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.IdToken))
        {
            return BadRequest(new { message = "IdToken is required." });
        }

        if (!_externalValidators.TryGetValue(provider, out var validator))
        {
            return StatusCode(501, new { message = "Provider is not configured." });
        }

        ExternalUserInfo? info;
        try
        {
            info = await validator.ValidateAsync(request.IdToken, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "External login token validation failed for provider {Provider}", provider);
            return Unauthorized();
        }

        if (info is null)
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(info.Email))
        {
            return BadRequest(new { message = "Email claim is required for external login." });
        }

        var email = info.Email.Trim().ToLowerInvariant();
        var user = await _db.Users
            .Include(x => x.UserRoles).ThenInclude(x => x.Role)
            .Include(x => x.TenantScopes)
            .Include(x => x.TenantAccess)
            .FirstOrDefaultAsync(x => x.Email.ToLower() == email, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        if (user is null)
        {
            user = new User
            {
                Email = email,
                DisplayName = string.IsNullOrWhiteSpace(info.Name) ? email.Split('@')[0] : info.Name,
                PasswordHash = PasswordHasher.Hash(Guid.NewGuid().ToString("N")),
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            };

            var defaultRole = _configuration["Auth:ExternalDefaultRole"] ?? "customer";
            var role = await _db.Roles.FirstOrDefaultAsync(x => x.Name == defaultRole, cancellationToken);
            if (role is not null)
            {
                user.UserRoles.Add(new UserRole { RoleId = role.Id, User = user });
            }

            _db.Users.Add(user);
            await _db.SaveChangesAsync(cancellationToken);
        }
        else if (!user.IsActive)
        {
            return Unauthorized();
        }

        var roles = await ResolveRolesAsync(user, cancellationToken);
        if (!TryResolveScope(user, roles, request.TenantId, request.PartnerId, out var tenantId, out var partnerId, out var error))
        {
            return error!;
        }

        var accessToken = _tokenService.CreateAccessToken(user, roles, tenantId, partnerId, now);
        var refresh = await IssueRefreshToken(user, now);
        var accessMinutes = _configuration.GetValue("Jwt:AccessTokenMinutes", 30);

        var response = new AuthResponse(
            AccessToken: accessToken,
            RefreshToken: refresh.RawToken,
            ExpiresIn: accessMinutes * 60,
            TokenType: "Bearer",
            User: new AuthUserDto(user.Id, user.Email, user.DisplayName, roles, tenantId, partnerId));

        return Ok(response);
    }

    private async Task<List<string>> ResolveRolesAsync(User user, CancellationToken cancellationToken)
    {
        var roles = user.UserRoles
            .Where(x => x.Role is not null && !string.IsNullOrWhiteSpace(x.Role.Name))
            .Select(x => x.Role!.Name)
            .ToList();

        if (roles.Count > 0)
        {
            return roles;
        }

        var defaultRoleName = _configuration["Auth:RegisterDefaultRole"]
            ?? _configuration["Auth:ExternalDefaultRole"]
            ?? "customer";
        var roleEntity = await _db.Roles.FirstOrDefaultAsync(x => x.Name == defaultRoleName, cancellationToken);
        if (roleEntity is null)
        {
            _logger.LogWarning("Default role {Role} not found in ACL database for user {UserId}", defaultRoleName, user.Id);
            return roles;
        }

        _db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = roleEntity.Id });
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        roles.Add(roleEntity.Name);
        return roles;
    }
}
