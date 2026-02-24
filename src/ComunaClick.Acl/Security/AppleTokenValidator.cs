namespace ComunaClick.Acl.Security;

public sealed class AppleTokenValidator : OpenIdTokenValidator
{
    public AppleTokenValidator(IConfiguration configuration, HttpClient httpClient)
        : base(
            provider: "apple",
            metadataAddress: "https://appleid.apple.com/.well-known/openid-configuration",
            issuer: "https://appleid.apple.com",
            validAudiences: configuration.GetSection("Auth:Apple:ClientIds").Get<string[]>() ??
                            (configuration["Auth:Apple:ClientId"] is { Length: > 0 } single ? new[] { single } : Array.Empty<string>()),
            httpClient: httpClient)
    {
    }
}
