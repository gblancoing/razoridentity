namespace ComunaClick.Acl.Contracts.Auth;

public sealed record LoginRequest(
    string Email,
    string Password,
    Guid? TenantId,
    Guid? PartnerId,
    string? RecaptchaToken);
