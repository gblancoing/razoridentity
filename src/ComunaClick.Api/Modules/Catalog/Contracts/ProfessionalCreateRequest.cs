namespace ComunaClick.Api.Modules.Catalog.Contracts;

public sealed record ProfessionalCreateRequest(
    string Name,
    string? Email,
    string? Phone,
    string? Specialty,
    string? Bio,
    bool? IsVerified,
    bool? IsActive);
