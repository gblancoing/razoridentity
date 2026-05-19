using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace ComunaClick.Acl.Security;

public abstract class OpenIdTokenValidator : IExternalTokenValidator
{
    private readonly ConfigurationManager<OpenIdConnectConfiguration> _configurationManager;
    private readonly string[] _validAudiences;
    private readonly string[] _validIssuers;

    protected OpenIdTokenValidator(
        string provider,
        string metadataAddress,
        string issuer,
        IEnumerable<string> validAudiences,
        HttpClient httpClient,
        IEnumerable<string>? validIssuers = null)
    {
        Provider = provider;
        _validAudiences = validAudiences.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToArray();
        _validIssuers = (validIssuers ?? new[] { issuer })
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        _configurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
            metadataAddress,
            new OpenIdConnectConfigurationRetriever(),
            new HttpDocumentRetriever(httpClient) { RequireHttps = true });
    }

    public string Provider { get; }

    public async Task<ExternalUserInfo?> ValidateAsync(string idToken, CancellationToken cancellationToken)
    {
        if (_validAudiences.Length == 0)
        {
            return null;
        }

        var config = await _configurationManager.GetConfigurationAsync(cancellationToken);
        var handler = new JwtSecurityTokenHandler
        {
            // Keep original JWT claim names (sub/email/name) to avoid missing-claim failures.
            MapInboundClaims = false
        };
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuers = _validIssuers,
            ValidateAudience = true,
            ValidAudiences = _validAudiences,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = config.SigningKeys,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2)
        };

        var principal = handler.ValidateToken(idToken, parameters, out _);
        var subject = principal.FindFirst("sub")?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(subject))
        {
            return null;
        }

        var email = principal.FindFirst("email")?.Value
            ?? principal.FindFirst(ClaimTypes.Email)?.Value;
        var name = principal.FindFirst("name")?.Value
            ?? principal.FindFirst(ClaimTypes.Name)?.Value
            ?? principal.FindFirst("given_name")?.Value;

        return new ExternalUserInfo(Provider, subject, email, name);
    }
}
