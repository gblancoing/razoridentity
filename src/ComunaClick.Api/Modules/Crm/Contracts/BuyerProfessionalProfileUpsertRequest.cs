namespace ComunaClick.Api.Modules.Crm.Contracts;

public sealed record BuyerProfessionalProfileUpsertRequest(
    Guid? TenantId,
    string? Name,
    string? Phone,
    string? Specialty,
    string? Bio,
    bool? Activate,
    string? WebsiteUrl = null,
    string? InstagramUrl = null,
    string? FacebookUrl = null,
    string? LinkedInUrl = null,
    string? XUrl = null,
    string? TikTokUrl = null,
    string? YouTubeUrl = null,
    string? OtherLinkLabel = null,
    string? OtherLinkUrl = null);
