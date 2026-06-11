namespace ComunaClick.Api.Modules.Onboarding.Contracts.Partners;

public sealed record PartnerUpdateRequest(
    string? Type,
    string? Name,
    string? Rut,
    string? Address,
    string? Phone,
    string? Email,
    Guid? SubcategoryId,
    double? Latitude,
    double? Longitude,
    bool? IsVisible,
    bool? OffersServices,
    string? BankName = null,
    string? BankAccountType = null,
    string? BankAccountNumber = null,
    string? BankAccountHolder = null,
    string? BankAccountHolderRut = null);
