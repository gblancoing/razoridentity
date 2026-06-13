using ComunaClick.Shared.Api.Buyer;

namespace ComunaClick.SharedUI.Services;

public static class SearchDisplayHelper
{
    public static string GetResultHref(SearchResultItem item)
    {
        var kind = item.Type?.Trim().ToLowerInvariant();
        return kind switch
        {
            "partner" => $"/buyer/detail/partner/{item.Id}",
            "service" => $"/buyer/detail/service/{item.Id}",
            "professional" => $"/buyer/detail/professional/{item.Id}",
            "product" => $"/buyer/detail/product/{item.Id}",
            _ => !string.IsNullOrWhiteSpace(item.CtaHref) ? item.CtaHref! : "#"
        };
    }

    public static string GetTypeIcon(string? type) => type?.Trim().ToLowerInvariant() switch
    {
        "partner" => "storefront",
        "product" => "shopping_bag",
        "service" => "home_repair_service",
        "professional" => "person",
        _ => "search"
    };

    public static string GetTypeLabel(LocaleService l, string? type) => type?.Trim().ToLowerInvariant() switch
    {
        "partner" => l.T("search.result.type.partner"),
        "product" => l.T("search.result.type.product"),
        "service" => l.T("search.result.type.service"),
        "professional" => l.T("search.result.type.professional"),
        _ => l.T("search.result.type.generic")
    };

    public static string? FormatCategorySubtitle(LocaleService l, SearchResultItem item)
    {
        var kind = item.Type?.Trim().ToLowerInvariant();
        if (kind == "partner")
        {
            return item.Category?.Trim().ToUpperInvariant() switch
            {
                "A" => l.T("search.result.partnerType.commerce"),
                "B" => l.T("search.result.partnerType.services"),
                "C" => l.T("search.result.partnerType.professionals"),
                _ => l.T("search.result.type.partner")
            };
        }

        return string.IsNullOrWhiteSpace(item.Category) ? GetTypeLabel(l, kind) : item.Category.Trim();
    }

    public static string? FormatDistance(LocaleService l, SearchResultItem item)
    {
        if (item.DistanceKm is not double km)
        {
            return null;
        }

        var rounded = km < 10 ? Math.Round(km, 1) : Math.Round(km);
        return string.Format(System.Globalization.CultureInfo.CurrentCulture, l.T("home.search.distanceKm"), rounded);
    }

    public static string? FormatPrice(SearchResultItem item)
    {
        if (item.Price is null)
        {
            return null;
        }

        var cur = (item.Currency ?? string.Empty).Trim().ToUpperInvariant();
        var n = item.Price.Value;
        return cur switch
        {
            "CLP" or "CL$" => $"$ {n:N0}",
            "USD" or "US$" or "USD $" => $"US$ {n:N2}",
            _ => string.IsNullOrWhiteSpace(item.Currency) ? $"$ {n:N0}" : $"{item.Currency} {n:N0}"
        };
    }

    public static bool HasMapCoordinates(SearchResultItem item)
        => item.Latitude is >= -90 and <= 90
        && item.Longitude is >= -180 and <= 180;

    public static string GetCtaLabel(LocaleService l, SearchResultItem item)
    {
        if (!string.IsNullOrWhiteSpace(item.CtaLabel))
        {
            return item.CtaLabel!;
        }

        return item.Type?.Trim().ToLowerInvariant() switch
        {
            "product" => l.T("search.result.cta.buy"),
            "service" => l.T("search.result.cta.book"),
            "professional" => l.T("search.result.cta.profile"),
            _ => l.T("search.result.cta.view")
        };
    }

    public static string? ResolveCardImageUrl(SearchResultItem item, Func<string, string>? mediaResolve = null)
    {
        var raw = PublicOfferVisuals.ResolveImage(item.ImageUrl, item.Category, item.Name, item.ImageUrls);
        if (string.IsNullOrWhiteSpace(raw))
        {
            raw = item.LogoUrl;
        }

        if (string.IsNullOrWhiteSpace(raw))
        {
            raw = CategoryVisualService.ResolveImage(item.Category, item.Category);
        }

        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return mediaResolve is null ? raw.Trim() : mediaResolve(raw.Trim());
    }

    public static string BuildCardBackgroundStyle(string? imageUrl)
        => string.IsNullOrWhiteSpace(imageUrl)
            ? "background-image: linear-gradient(135deg, rgba(220,252,231,0.95) 0%, rgba(250,244,234,0.98) 55%, rgba(255,255,255,1) 100%);"
            : $"background-image: url('{imageUrl}');";

    private static readonly string[] AvatarPalette =
    [
        "bg-gradient-to-br from-emerald-500 to-emerald-700",
        "bg-gradient-to-br from-amber-500 to-orange-600",
        "bg-gradient-to-br from-sky-500 to-blue-700",
        "bg-gradient-to-br from-violet-500 to-purple-700",
        "bg-gradient-to-br from-rose-500 to-pink-700",
        "bg-gradient-to-br from-teal-500 to-cyan-700",
        "bg-gradient-to-br from-lime-500 to-green-700",
        "bg-gradient-to-br from-fuchsia-500 to-purple-600",
    ];

    public static string GetInitials(string? name)
    {
        var trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            return "?";
        }

        var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2
            ? $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[1][0])}"
            : trimmed.Length >= 2 ? trimmed[..2].ToUpperInvariant() : trimmed[..1].ToUpperInvariant();
    }

    public static string GetAvatarColorClass(string? name)
    {
        var seed = (name ?? string.Empty).Trim();
        if (seed.Length == 0)
        {
            return AvatarPalette[0];
        }

        var hash = 0;
        foreach (var c in seed)
        {
            hash = (hash * 31 + c) & 0x7fffffff;
        }

        return AvatarPalette[hash % AvatarPalette.Length];
    }
}
