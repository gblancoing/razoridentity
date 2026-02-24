namespace ComunaClick.Acl.Contracts.Auth;

public sealed record PasswordResetConfirmRequest(string Token, string NewPassword);
