namespace ComunaClick.Api.Modules.Onboarding.Contracts;

public sealed record PartnerStorefrontResponse(
  Guid PartnerId,
  string? BannerUrl,
  string? LogoUrl,
  string? StorefrontTagline,
  string? StorefrontAbout,
  string? StorefrontHighlight1,
  string? StorefrontHighlight2,
  string? StorefrontHighlight3,
  string PublicProfilePath,
  bool ShippingCourierPaidEnabled = false,
  bool ShippingFreeOverAmountEnabled = false,
  decimal? ShippingFreeOverAmount = null,
  bool ShippingDeliveryZoneEnabled = false,
  bool ShippingFreeEnabled = false);

public sealed record PartnerStorefrontUpdateRequest(
  string? StorefrontTagline,
  string? StorefrontAbout,
  string? StorefrontHighlight1,
  string? StorefrontHighlight2,
  string? StorefrontHighlight3,
  bool? RemoveBanner,
  bool? RemoveLogo);

public sealed record PartnerShippingMethodsUpdateRequest(
  bool CourierPaid,
  bool FreeOverAmount,
  decimal? FreeOverAmountValue,
  bool DeliveryZone,
  bool Free);

public sealed record ProfessionalStorefrontResponse(
  Guid ProfessionalId,
  string? Name,
  string? Specialty,
  string? BannerUrl,
  string? ProfileHeadline,
  string? Bio,
  string PublicProfilePath);

public sealed record ProfessionalStorefrontUpdateRequest(
  string? ProfileHeadline,
  string? Bio,
  bool? RemoveBanner);
