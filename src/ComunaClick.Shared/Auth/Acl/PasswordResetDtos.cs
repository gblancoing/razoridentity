namespace ComunaClick.Shared.Auth.Acl;

public sealed record PasswordResetStartRequest(string? Email);
public sealed record PasswordResetStartResponse(string? ResetToken);
public sealed record PasswordResetConfirmRequest(string? Token, string? NewPassword);
