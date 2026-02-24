namespace ComunaClick.Acl.Security;

public sealed record ExternalUserInfo(
    string Provider,
    string Subject,
    string? Email,
    string? Name);
