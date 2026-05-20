namespace ComunaClick.Api.Modules.Crm.Contracts;

public sealed record BuyerProfessionalProfileUpsertRequest(
    Guid? TenantId,
    string? Name,
    string? Phone,
    string? Specialty,
    string? Bio,
    bool? Activate);
