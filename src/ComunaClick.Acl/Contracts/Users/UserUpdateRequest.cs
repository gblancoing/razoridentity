namespace ComunaClick.Acl.Contracts.Users;

public sealed record UserUpdateRequest(string? Email, string? Password, string? DisplayName, bool? IsActive);
