using ComunaClick.Common;

namespace ComunaClick.SharedUI.Services;

public static class ProfileWebLinksHelper
{
    public static bool HasAny(ProfileWebLinks? links)
        => links is not null && GetEntries(links).Count > 0;

    public static ProfileWebLinks FromPartner(ComunaClick.Shared.Api.Partner.PartnerDto partner)
        => new(
            partner.WebsiteUrl,
            partner.InstagramUrl,
            partner.FacebookUrl,
            partner.LinkedInUrl,
            partner.XUrl,
            partner.TikTokUrl,
            partner.YouTubeUrl,
            partner.OtherLinkLabel,
            partner.OtherLinkUrl);

    public static ProfileWebLinks FromPartnerSummary(ComunaClick.Shared.Api.Buyer.PartnerProfileSummary partner)
        => new(
            partner.WebsiteUrl,
            partner.InstagramUrl,
            partner.FacebookUrl,
            partner.LinkedInUrl,
            partner.XUrl,
            partner.TikTokUrl,
            partner.YouTubeUrl,
            partner.OtherLinkLabel,
            partner.OtherLinkUrl);

    public static ProfileWebLinks FromProfessional(ComunaClick.Shared.Api.Buyer.Professional professional)
        => new(
            professional.WebsiteUrl,
            professional.InstagramUrl,
            professional.FacebookUrl,
            professional.LinkedInUrl,
            professional.XUrl,
            professional.TikTokUrl,
            professional.YouTubeUrl,
            professional.OtherLinkLabel,
            professional.OtherLinkUrl);

    public static ProfileWebLinks FromBuyerProfessional(ComunaClick.Shared.Api.Buyer.BuyerProfessionalProfile profile)
        => new(
            profile.WebsiteUrl,
            profile.InstagramUrl,
            profile.FacebookUrl,
            profile.LinkedInUrl,
            profile.XUrl,
            profile.TikTokUrl,
            profile.YouTubeUrl,
            profile.OtherLinkLabel,
            profile.OtherLinkUrl);

    public static ComunaClick.Shared.Api.Partner.ProfileWebLinksUpdateRequest ToUpdateRequest(ProfileWebLinksEditorModel model)
        => new(
            Null(model.WebsiteUrl),
            Null(model.InstagramUrl),
            Null(model.FacebookUrl),
            Null(model.LinkedInUrl),
            Null(model.XUrl),
            Null(model.TikTokUrl),
            Null(model.YouTubeUrl),
            Null(model.OtherLinkLabel),
            Null(model.OtherLinkUrl));

    public static void Apply(ProfileWebLinksEditorModel model, ProfileWebLinks? links)
    {
        model.WebsiteUrl = links?.WebsiteUrl ?? string.Empty;
        model.InstagramUrl = links?.InstagramUrl ?? string.Empty;
        model.FacebookUrl = links?.FacebookUrl ?? string.Empty;
        model.LinkedInUrl = links?.LinkedInUrl ?? string.Empty;
        model.XUrl = links?.XUrl ?? string.Empty;
        model.TikTokUrl = links?.TikTokUrl ?? string.Empty;
        model.YouTubeUrl = links?.YouTubeUrl ?? string.Empty;
        model.OtherLinkLabel = links?.OtherLinkLabel ?? string.Empty;
        model.OtherLinkUrl = links?.OtherLinkUrl ?? string.Empty;
    }

    public static IReadOnlyList<ProfileWebLinkEntry> GetEntries(ProfileWebLinks links)
    {
        var entries = new List<ProfileWebLinkEntry>();
        Add(entries, "website", links.WebsiteUrl, "language");
        Add(entries, "instagram", links.InstagramUrl, "photo_camera");
        Add(entries, "facebook", links.FacebookUrl, "groups");
        Add(entries, "linkedin", links.LinkedInUrl, "work");
        Add(entries, "x", links.XUrl, "tag");
        Add(entries, "tiktok", links.TikTokUrl, "music_note");
        Add(entries, "youtube", links.YouTubeUrl, "play_circle");
        if (!string.IsNullOrWhiteSpace(links.OtherLinkUrl))
        {
            entries.Add(new ProfileWebLinkEntry(
                "other",
                string.IsNullOrWhiteSpace(links.OtherLinkLabel) ? "other" : links.OtherLinkLabel.Trim(),
                links.OtherLinkUrl.Trim(),
                "link"));
        }

        return entries;
    }

    private static void Add(List<ProfileWebLinkEntry> entries, string key, string? url, string icon)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        entries.Add(new ProfileWebLinkEntry(key, key, url.Trim(), icon));
    }

    private static string? Null(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class ProfileWebLinksEditorModel
{
    public string WebsiteUrl { get; set; } = string.Empty;
    public string InstagramUrl { get; set; } = string.Empty;
    public string FacebookUrl { get; set; } = string.Empty;
    public string LinkedInUrl { get; set; } = string.Empty;
    public string XUrl { get; set; } = string.Empty;
    public string TikTokUrl { get; set; } = string.Empty;
    public string YouTubeUrl { get; set; } = string.Empty;
    public string OtherLinkLabel { get; set; } = string.Empty;
    public string OtherLinkUrl { get; set; } = string.Empty;
}

public sealed record ProfileWebLinkEntry(string Key, string LabelKey, string Url, string Icon);
