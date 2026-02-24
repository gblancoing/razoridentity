namespace ComunaClick.Shared.Auth;

public enum ExternalProvider
{
    Google,
    Apple
}

public sealed record ExternalLoginRequest(
    ExternalProvider Provider,
    string IdToken,
    string? AccessToken = null,
    string? AuthorizationCode = null,
    Guid? TenantId = null,
    Guid? PartnerId = null
);
