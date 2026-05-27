namespace ComunaClick.Common;

/// <summary>Enlaces públicos de sitio web y redes sociales en perfiles.</summary>
public sealed record ProfileWebLinks(
    string? WebsiteUrl = null,
    string? InstagramUrl = null,
    string? FacebookUrl = null,
    string? LinkedInUrl = null,
    string? XUrl = null,
    string? TikTokUrl = null,
    string? YouTubeUrl = null,
    string? OtherLinkLabel = null,
    string? OtherLinkUrl = null);
