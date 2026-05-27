namespace ComunaClick.Api.Modules.Onboarding.Contracts.Partners;

public sealed record PartnerBankAccountUpdateRequest(
    string BankName,
    string BankAccountType,
    string BankAccountNumber,
    string BankAccountHolder,
    string? BankAccountHolderRut);
