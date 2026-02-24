namespace ComunaClick.Api.Modules.Onboarding.Contracts.Partners;

public sealed record PartnerStaffCreateRequest(Guid UserId, string? Role);
