namespace ComunaClick.Shared.Auth;

public sealed record RegisterRequest(
    string Name,
    string Email,
    string Password,
    string? RecaptchaToken = null);
