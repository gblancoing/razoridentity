namespace ComunaClick.Acl.Security;

public sealed class GoogleTokenValidator : OpenIdTokenValidator
{
    public GoogleTokenValidator(IConfiguration configuration, HttpClient httpClient)
        : base(
            provider: "google",
            metadataAddress: "https://accounts.google.com/.well-known/openid-configuration",
            issuer: "https://accounts.google.com",
            validAudiences: configuration.GetSection("Auth:Google:ClientIds").Get<string[]>() ??
                            (configuration["Auth:Google:ClientId"] is { Length: > 0 } single ? new[] { single } : Array.Empty<string>()),
            httpClient: httpClient)
    {
    }
}
