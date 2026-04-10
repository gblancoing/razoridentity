namespace ComunaClick.Admin.Models;

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

public sealed record AdminTenantListItemDto(
    Guid Id,
    string Name,
    string Timezone,
    string? ComunaName,
    bool IsActive,
    int PartnerCount,
    DateTimeOffset UpdatedAt);

public sealed record AdminCategoryListItemDto(
    Guid Id,
    string Code,
    string Name,
    int SortOrder,
    bool IsActive,
    int SubcategoryCount);

public sealed class SiteContentDto
{
    public HomeContentDto Home { get; set; } = new();
    public FooterContentDto Footer { get; set; } = new();
}

public sealed class HomeContentDto
{
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string BackgroundImageUrl { get; set; } = string.Empty;
    public string PrimaryCtaLabel { get; set; } = string.Empty;
    public string PrimaryCtaHref { get; set; } = string.Empty;
    public string SecondaryCtaLabel { get; set; } = string.Empty;
    public string SecondaryCtaHref { get; set; } = string.Empty;
}

public sealed class FooterContentDto
{
    public string CopyrightText { get; set; } = string.Empty;
    public string InstagramUrl { get; set; } = string.Empty;
    public string FacebookUrl { get; set; } = string.Empty;
    public string LinkedInUrl { get; set; } = string.Empty;
}

public sealed record AdminAuditEventDto(
    string EntityType,
    string EntityId,
    string Action,
    string Description,
    DateTimeOffset OccurredAt);

public sealed record AdminUserDto(
    Guid Id,
    string Email,
    string? DisplayName,
    bool IsActive,
    IReadOnlyList<string> Roles);

public sealed record AdminRoleDto(
    Guid Id,
    string Name);

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
