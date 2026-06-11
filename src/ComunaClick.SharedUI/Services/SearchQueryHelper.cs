using ComunaClick.Shared.Api.Buyer;

namespace ComunaClick.SharedUI.Services;

public static class SearchQueryHelper
{
    public const int HomePreviewCount = 3;
    /// <summary>Fetch one extra on home to detect if a "view all" link is needed.</summary>
    public const int HomeFetchLimit = 4;
    public const int FullPageLimit = 50;

    public static string BuildBuscarUrl(string? query, string? type = null, GeoFilter? geo = null, string? view = null)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(query))
        {
            parts.Add($"q={Uri.EscapeDataString(query.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(type))
        {
            parts.Add($"type={Uri.EscapeDataString(type.Trim())}");
        }

        if (geo?.HasCoordinates == true)
        {
            parts.Add($"lat={geo.Latitude!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            parts.Add($"lng={geo.Longitude!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            var radius = geo.RadiusKm ?? 30;
            parts.Add($"radiusKm={radius.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
        }

        if (!string.IsNullOrWhiteSpace(view) && !view.Equals("list", StringComparison.OrdinalIgnoreCase))
        {
            parts.Add($"view={Uri.EscapeDataString(view.Trim())}");
        }

        return parts.Count == 0 ? "/buscar" : $"/buscar?{string.Join("&", parts)}";
    }

    public static string? ParseViewFromUri(Uri uri)
    {
        var query = uri.Query;
        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        query = query.TrimStart('?');
        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (!string.Equals(Uri.UnescapeDataString(parts[0]), "view", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : null;
        }

        return null;
    }

    public static GeoFilter? ParseGeoFromUri(Uri uri)
    {
        var query = uri.Query;
        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        query = query.TrimStart('?');
        double? ReadDouble(string key)
        {
            foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = pair.Split('=', 2);
                if (!string.Equals(Uri.UnescapeDataString(parts[0]), key, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return parts.Length > 1
                    && double.TryParse(
                        Uri.UnescapeDataString(parts[1]),
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out var value)
                    ? value
                    : null;
            }

            return null;
        }

        Guid? ReadGuid(string key)
        {
            foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = pair.Split('=', 2);
                if (!string.Equals(Uri.UnescapeDataString(parts[0]), key, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return parts.Length > 1 && Guid.TryParse(Uri.UnescapeDataString(parts[1]), out var parsed) ? parsed : null;
            }

            return null;
        }

        var lat = ReadDouble("lat") ?? ReadDouble("latitude");
        var lng = ReadDouble("lng") ?? ReadDouble("longitude");
        var radius = ReadDouble("radiusKm") ?? 30;

        if (lat is >= -90 and <= 90 && lng is >= -180 and <= 180
            && GeoLocationValidator.IsPlausibleSearchOrigin(lat.Value, lng.Value))
        {
            return new GeoFilter(Latitude: lat, Longitude: lng, RadiusKm: radius);
        }

        var countryId = ReadGuid("countryId");
        var regionId = ReadGuid("regionId");
        var comunaId = ReadGuid("comunaId");
        return countryId.HasValue || regionId.HasValue || comunaId.HasValue
            ? new GeoFilter(countryId, regionId, comunaId)
            : null;
    }

    public static string? ParseQueryFromUri(Uri uri)
    {
        var query = uri.Query;
        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        query = query.TrimStart('?');
        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (!string.Equals(Uri.UnescapeDataString(parts[0]), "q", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty;
        }

        return null;
    }

    public static string? ParseTypeFromUri(Uri uri)
    {
        var query = uri.Query;
        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        query = query.TrimStart('?');
        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (!string.Equals(Uri.UnescapeDataString(parts[0]), "type", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : null;
        }

        return null;
    }
}
