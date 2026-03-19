namespace ComunaClick.Api.Modules.Onboarding.Contracts.Partners;

public sealed record PartnerCreateRequest(
    string Type,
    string Name,
    string? Rut,
    string? Address,
    string? Phone,
    string? Email,
    Guid? SubcategoryId);
