namespace ComunaClick.Acl.Contracts.Users;

public sealed record UserCreateRequest(string Email, string Password, string? DisplayName, bool IsActive = true);
