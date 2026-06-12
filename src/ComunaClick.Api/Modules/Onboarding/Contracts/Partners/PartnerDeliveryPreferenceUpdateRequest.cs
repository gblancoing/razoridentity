namespace ComunaClick.Api.Modules.Onboarding.Contracts.Partners;

/// <summary>Transportista preferido del comercio; null = automático por zona.</summary>
public sealed record PartnerDeliveryPreferenceUpdateRequest(Guid? PreferredDeliveryProviderId);
