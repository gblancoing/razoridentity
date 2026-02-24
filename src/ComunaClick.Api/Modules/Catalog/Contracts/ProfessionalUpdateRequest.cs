namespace ComunaClick.Api.Modules.Catalog.Contracts;

public sealed record ProfessionalUpdateRequest(
    string? Name,
    string? Email,
    string? Phone,
    string? Specialty,
    string? Bio,
    bool? IsVerified,
    bool? IsActive);
