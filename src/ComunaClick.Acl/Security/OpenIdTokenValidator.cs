using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace ComunaClick.Acl.Security;

public abstract class OpenIdTokenValidator : IExternalTokenValidator
{
    private readonly ConfigurationManager<OpenIdConnectConfiguration> _configurationManager;
    private readonly string[] _validAudiences;
    private readonly string _issuer;

    protected OpenIdTokenValidator(string provider, string metadataAddress, string issuer, IEnumerable<string> validAudiences, HttpClient httpClient)
    {
        Provider = provider;
        _issuer = issuer;
        _validAudiences = validAudiences.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToArray();

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
        var handler = new JwtSecurityTokenHandler();
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _issuer,
            ValidateAudience = true,
            ValidAudiences = _validAudiences,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = config.SigningKeys,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2)
        };

        var principal = handler.ValidateToken(idToken, parameters, out _);
        var subject = principal.FindFirst("sub")?.Value;
        if (string.IsNullOrWhiteSpace(subject))
        {
            return null;
        }

        var email = principal.FindFirst("email")?.Value;
        var name = principal.FindFirst("name")?.Value
            ?? principal.FindFirst("given_name")?.Value;

        return new ExternalUserInfo(Provider, subject, email, name);
    }
}
