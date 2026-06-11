using ComunaClick.Common;
using ComunaClick.Api.Modules.Onboarding.Contracts;

namespace ComunaClick.Api.Modules.Onboarding;

public static class ProfileWebLinksNormalizer
{
    public static ProfileWebLinks FromPartner(ComunaClick.Api.Persistence.Entities.Partner partner)
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

    public static ProfileWebLinks FromProfessional(ComunaClick.Api.Persistence.Entities.Professional professional)
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

    public static void ApplyToPartner(
        ComunaClick.Api.Persistence.Entities.Partner partner,
        ProfileWebLinksUpdateRequest request)
    {
        if (request.WebsiteUrl is not null)
        {
            partner.WebsiteUrl = NormalizeUrl(request.WebsiteUrl);
        }

        if (request.InstagramUrl is not null)
        {
            partner.InstagramUrl = NormalizeUrl(request.InstagramUrl);
        }

        if (request.FacebookUrl is not null)
        {
            partner.FacebookUrl = NormalizeUrl(request.FacebookUrl);
        }

        if (request.LinkedInUrl is not null)
        {
            partner.LinkedInUrl = NormalizeUrl(request.LinkedInUrl);
        }

        if (request.XUrl is not null)
        {
            partner.XUrl = NormalizeUrl(request.XUrl);
        }

        if (request.TikTokUrl is not null)
        {
            partner.TikTokUrl = NormalizeUrl(request.TikTokUrl);
        }

        if (request.YouTubeUrl is not null)
        {
            partner.YouTubeUrl = NormalizeUrl(request.YouTubeUrl);
        }

        if (request.OtherLinkLabel is not null)
        {
            partner.OtherLinkLabel = NormalizeLabel(request.OtherLinkLabel);
        }

        if (request.OtherLinkUrl is not null)
        {
            partner.OtherLinkUrl = NormalizeUrl(request.OtherLinkUrl);
        }
    }

    public static void ApplyToProfessional(
        ComunaClick.Api.Persistence.Entities.Professional professional,
        ProfileWebLinksUpdateRequest request)
    {
        if (request.WebsiteUrl is not null)
        {
            professional.WebsiteUrl = NormalizeUrl(request.WebsiteUrl);
        }

        if (request.InstagramUrl is not null)
        {
            professional.InstagramUrl = NormalizeUrl(request.InstagramUrl);
        }

        if (request.FacebookUrl is not null)
        {
            professional.FacebookUrl = NormalizeUrl(request.FacebookUrl);
        }

        if (request.LinkedInUrl is not null)
        {
            professional.LinkedInUrl = NormalizeUrl(request.LinkedInUrl);
        }

        if (request.XUrl is not null)
        {
            professional.XUrl = NormalizeUrl(request.XUrl);
        }

        if (request.TikTokUrl is not null)
        {
            professional.TikTokUrl = NormalizeUrl(request.TikTokUrl);
        }

        if (request.YouTubeUrl is not null)
        {
            professional.YouTubeUrl = NormalizeUrl(request.YouTubeUrl);
        }

        if (request.OtherLinkLabel is not null)
        {
            professional.OtherLinkLabel = NormalizeLabel(request.OtherLinkLabel);
        }

        if (request.OtherLinkUrl is not null)
        {
            professional.OtherLinkUrl = NormalizeUrl(request.OtherLinkUrl);
        }
    }

    public static string? Validate(ProfileWebLinksUpdateRequest request)
    {
        var urls = new[]
        {
            (request.WebsiteUrl, "sitio web"),
            (request.InstagramUrl, "Instagram"),
            (request.FacebookUrl, "Facebook"),
            (request.LinkedInUrl, "LinkedIn"),
            (request.XUrl, "X"),
            (request.TikTokUrl, "TikTok"),
            (request.YouTubeUrl, "YouTube"),
            (request.OtherLinkUrl, "enlace adicional")
        };

        foreach (var (raw, label) in urls)
        {
            if (raw is null)
            {
                continue;
            }

            var trimmed = raw.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                continue;
            }

            var normalized = NormalizeUrl(trimmed);
            if (normalized is null || !Uri.TryCreate(normalized, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                return $"El enlace de {label} no es válido. Usá una URL completa (https://…).";
            }
        }

        if (request.OtherLinkLabel is not null
            && !string.IsNullOrWhiteSpace(request.OtherLinkUrl)
            && string.IsNullOrWhiteSpace(request.OtherLinkLabel.Trim())
            && !string.IsNullOrWhiteSpace(NormalizeUrl(request.OtherLinkUrl)))
        {
            return "Indicá un nombre para el enlace adicional.";
        }

        return null;
    }

    private static string? NormalizeLabel(string? value)
    {
        if (value is null)
        {
            return null;
        }

        var trimmed = value.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static string? NormalizeUrl(string? value)
    {
        if (value is null)
        {
            return null;
        }

        var trimmed = value.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        if (!trimmed.Contains("://", StringComparison.Ordinal))
        {
            trimmed = "https://" + trimmed;
        }

        return trimmed;
    }
}
