using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ComunaClick.Common.Auth;
using ComunaClick.Acl.Domain;
using Microsoft.IdentityModel.Tokens;

namespace ComunaClick.Acl.Security;

public sealed class JwtTokenService
{
    private readonly IConfiguration _configuration;

    public JwtTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string CreateAccessToken(User user, IReadOnlyList<string> roles, Guid? tenantId, Guid? partnerId, DateTimeOffset now)
    {
        var issuer = _configuration["Jwt:Issuer"] ?? throw new InvalidOperationException("Jwt:Issuer is required.");
        var audience = _configuration["Jwt:Audience"] ?? throw new InvalidOperationException("Jwt:Audience is required.");
        var signingKey = _configuration["Jwt:SigningKey"] ?? throw new InvalidOperationException("Jwt:SigningKey is required.");
        var accessMinutes = _configuration.GetValue("Jwt:AccessTokenMinutes", 30);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (!string.IsNullOrWhiteSpace(user.DisplayName))
        {
            claims.Add(new Claim(AuthConstants.ClaimName, user.DisplayName));
        }

        foreach (var role in roles)
        {
            claims.Add(new Claim(AuthConstants.ClaimRole, role));
            claims.Add(new Claim(AuthConstants.ClaimRoles, role));
        }

        if (user.IsSuperAdmin)
        {
            claims.Add(new Claim(AuthConstants.ClaimIsSuperAdmin, "true"));
        }

        if (tenantId.HasValue)
        {
            claims.Add(new Claim(AuthConstants.ClaimTenantId, tenantId.Value.ToString()));
        }

        if (partnerId.HasValue)
        {
            claims.Add(new Claim(AuthConstants.ClaimPartnerId, partnerId.Value.ToString()));
        }

        var key = new SymmetricSecurityKey(JwtKey.GetKeyBytes(signingKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = now.AddMinutes(accessMinutes);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
