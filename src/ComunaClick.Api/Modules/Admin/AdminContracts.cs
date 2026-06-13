namespace ComunaClick.Api.Modules.Admin;

public sealed record AdminDashboardDto(
    int TenantCount,
    int PartnerCount,
    int VisiblePartnerCount,
    int CategoryCount,
    int OrderCount,
    int BookingCount,
    int LeadCount,
    IReadOnlyList<AdminRecentPartnerDto> RecentPartners,
    IReadOnlyList<AdminRecentOrderDto> RecentOrders);

public sealed record AdminRecentPartnerDto(
    Guid Id,
    string Name,
    string Type,
    bool IsVisible,
    DateTimeOffset UpdatedAt);

public sealed record AdminRecentOrderDto(
    Guid Id,
    Guid PartnerId,
    string Status,
    decimal TotalAmount,
    string Currency,
    DateTimeOffset CreatedAt);

public sealed record AdminPartnerListItemDto(
    Guid Id,
    Guid TenantId,
    string Type,
    string Name,
    string? CategoryName,
    string? SubcategoryName,
    string? Address,
    string? Phone,
    string? Email,
    bool IsVisible,
    DateTimeOffset UpdatedAt);

public sealed record AdminPartnerUpdateRequest(
    string? Name,
    string? Address,
    string? Phone,
    string? Email,
    bool? IsVisible);

public sealed record AdminTenantListItemDto(
    Guid Id,
    string Name,
    string Timezone,
    string? ComunaName,
    bool IsActive,
    int PartnerCount,
    DateTimeOffset UpdatedAt);

public sealed record AdminTenantUpdateRequest(
    string? Name,
    bool? IsActive,
    string? Timezone);

public sealed record AdminCategoryListItemDto(
    Guid Id,
    string Code,
    string Name,
    string? ImageUrl,
    int SortOrder,
    bool IsActive,
    int SubcategoryCount);

public sealed record AdminCategoryUpdateRequest(
    string? Name,
    int? SortOrder,
    bool? IsActive);

public sealed record SiteContentDto(
    HomeContentDto Home,
    FooterContentDto Footer);

public sealed record HomeContentDto(
    string Title,
    string Subtitle,
    string BackgroundImageUrl,
    string PrimaryCtaLabel,
    string PrimaryCtaHref,
    string SecondaryCtaLabel,
    string SecondaryCtaHref);

public sealed record FooterContentDto(
    string CopyrightText,
    string InstagramUrl,
    string FacebookUrl,
    string LinkedInUrl);

public sealed record SiteContentUpdateRequest(
    HomeContentDto Home,
    FooterContentDto Footer);

public sealed record AdminAuditEventDto(
    string EntityType,
    string EntityId,
    string Action,
    string Description,
    DateTimeOffset OccurredAt);

public sealed record AdminDeliveryProviderDto(
    Guid Id,
    Guid? TenantId,
    Guid? RegionId,
    Guid? ComunaId,
    string Name,
    string? ContactName,
    string? ContactPhone,
    string? ContactEmail,
    decimal BaseFee,
    int? EstimatedMinutes,
    bool IsActive,
    DateTimeOffset UpdatedAt);

public sealed record AdminSellerFeeItemDto(
    Guid SellerId,
    string SellerName,
    decimal FixedFeeAmount,
    decimal PercentageFee,
    bool IsActive);

public sealed record AdminGlobalFeeUpdateRequest(
    decimal PercentageFee,
    decimal FixedFeeAmount);

public sealed record AdminSellerFeeUpdateRequest(
    decimal PercentageFee,
    decimal FixedFeeAmount);

public sealed record AdminDeliveryProviderUpsertRequest(
    Guid? TenantId,
    Guid? RegionId,
    Guid? ComunaId,
    string Name,
    string? ContactName,
    string? ContactPhone,
    string? ContactEmail,
    decimal? BaseFee,
    int? EstimatedMinutes,
    bool? IsActive);
