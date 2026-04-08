namespace ComunaClick.Admin.Models;

public sealed record PaymentsAuthTokens(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAt,
    Guid? TenantId,
    Guid? PartnerId,
    string? DisplayName,
    string? Email,
    IReadOnlyList<string> Roles);
