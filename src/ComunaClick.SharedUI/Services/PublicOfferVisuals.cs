using ComunaClick.Shared.Api.Buyer;

namespace ComunaClick.SharedUI.Services;

public static class PublicOfferVisuals
{
    public static bool IsBookable(double price, bool isBookable = false)
        => isBookable || price > 0;

    public static bool IsBookable(PartnerProfileService service)
        => IsBookable(service.Price, service.IsBookable);

    public static bool IsBookable(Service service)
        => IsBookable(service.Price, service.IsBookable);

    public static string FormatMoney(double amount, string? currency)
        => string.IsNullOrWhiteSpace(currency) ? $"${amount:N0}" : $"{currency} {amount:N0}";

    public static string ResolveImage(string? imageUrl, string? categoryCodeOrName, string? itemName, IReadOnlyList<string>? imageUrls = null)
    {
        if (imageUrls is { Count: > 0 })
        {
            var first = imageUrls.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
            if (!string.IsNullOrWhiteSpace(first))
            {
                return first.Trim();
            }
        }

        if (!string.IsNullOrWhiteSpace(imageUrl))
        {
            return imageUrl.Trim();
        }

        return CategoryVisualService.ResolveImage(categoryCodeOrName, itemName)
            ?? "_content/ComunaClick.SharedUI/logo.png";
    }

    public static IReadOnlyList<string> ResolveGallery(string? imageUrl, IReadOnlyList<string>? imageUrls, string? category, string? name)
    {
        var list = new List<string>();
        if (imageUrls is { Count: > 0 })
        {
            foreach (var url in imageUrls)
            {
                if (!string.IsNullOrWhiteSpace(url))
                {
                    list.Add(url.Trim());
                }
            }
        }

        if (list.Count == 0 && !string.IsNullOrWhiteSpace(imageUrl))
        {
            list.Add(imageUrl.Trim());
        }

        if (list.Count == 0)
        {
            var fallback = CategoryVisualService.ResolveImage(category, name);
            if (!string.IsNullOrWhiteSpace(fallback))
            {
                list.Add(fallback);
            }
        }

        return list;
    }

    public static string? ResolveServiceLocation(string? serviceAddress, string? partnerAddress)
    {
        if (!string.IsNullOrWhiteSpace(serviceAddress))
        {
            return serviceAddress.Trim();
        }

        return string.IsNullOrWhiteSpace(partnerAddress) ? null : partnerAddress.Trim();
    }

    public static string BackgroundStyle(string imageUrl)
        => $"background-image: linear-gradient(180deg, rgba(10,12,10,0.05) 0%, rgba(10,12,10,0.55) 100%), url('{imageUrl}'); background-size: cover; background-position: center;";

    public static string CategoryLabel(string? category)
        => string.IsNullOrWhiteSpace(category) ? "General" : category.Trim();
}
