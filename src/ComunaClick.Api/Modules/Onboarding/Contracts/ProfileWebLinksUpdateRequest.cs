namespace ComunaClick.Api.Modules.Onboarding.Contracts;

public sealed record ProfileWebLinksUpdateRequest(
    string? WebsiteUrl,
    string? InstagramUrl,
    string? FacebookUrl,
    string? LinkedInUrl,
    string? XUrl,
    string? TikTokUrl,
    string? YouTubeUrl,
    string? OtherLinkLabel,
    string? OtherLinkUrl);
