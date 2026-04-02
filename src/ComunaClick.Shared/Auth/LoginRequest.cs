namespace ComunaClick.Shared.Auth;

public sealed record LoginRequest(
    string Email,
    string Password,
    Guid? TenantId = null,
    Guid? PartnerId = null,
    string? RecaptchaToken = null);
