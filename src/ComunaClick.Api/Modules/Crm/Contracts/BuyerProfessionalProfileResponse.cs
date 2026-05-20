namespace ComunaClick.Api.Modules.Crm.Contracts;

public sealed record BuyerProfessionalProfileResponse(
    bool HasProfile,
    Guid? ProfessionalId,
    string? Name,
    string? Email,
    string? Phone,
    string? Specialty,
    string? Bio,
    bool IsVerified,
    bool IsActive);
