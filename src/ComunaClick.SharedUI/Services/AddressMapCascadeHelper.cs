using Microsoft.JSInterop;

namespace ComunaClick.SharedUI.Services;

/// <summary>Niveles de precisión del mapa según la cascada País → Región → Comuna → Calle → Número.</summary>
public enum AddressCascadeLevel
{
    None = 0,
    Country = 1,
    Region = 2,
    Comuna = 3,
    Street = 4,
    Number = 5
}

public readonly record struct AddressCascadeContext(
    string? CountryName,
    string? RegionName,
    string? ComunaName,
    double? ComunaLatitude,
    double? ComunaLongitude,
    string? Street,
    string? Number);

public readonly record struct AddressCascadeResult(double Latitude, double Longitude, AddressCascadeLevel Level);

public static class AddressMapCascadeHelper
{
    public const string ResetManualAdjustJs = "comunaclic.resetProfileAddressManualAdjust";
    public const string FocusMapJs = "comunaclic.profileAddressFocusMap";
    public const string GeocodeQueryJs = "comunaclic.geocodeAddressQuery";
    public const string GeocodeStructuredJs = "comunaclic.geocodeStructuredAddress";

    public static AddressCascadeLevel ResolveLevel(AddressCascadeContext ctx)
    {
        var street = ctx.Street?.Trim() ?? string.Empty;
        var number = ctx.Number?.Trim() ?? string.Empty;

        if (street.Length >= 2 && number.Length > 0)
        {
            return AddressCascadeLevel.Number;
        }

        if (street.Length >= 2)
        {
            return AddressCascadeLevel.Street;
        }

        if (!string.IsNullOrWhiteSpace(ctx.ComunaName))
        {
            return AddressCascadeLevel.Comuna;
        }

        if (!string.IsNullOrWhiteSpace(ctx.RegionName))
        {
            return AddressCascadeLevel.Region;
        }

        if (!string.IsNullOrWhiteSpace(ctx.CountryName))
        {
            return AddressCascadeLevel.Country;
        }

        return AddressCascadeLevel.None;
    }

    public static int ZoomFor(AddressCascadeLevel level) => level switch
    {
        AddressCascadeLevel.Country => 6,
        AddressCascadeLevel.Region => 9,
        AddressCascadeLevel.Comuna => 13,
        AddressCascadeLevel.Street => 15,
        AddressCascadeLevel.Number => 17,
        _ => 11
    };

    public static bool MovesMarker(AddressCascadeLevel level) =>
        level >= AddressCascadeLevel.Comuna;

    public static string? BuildFreeTextQuery(AddressCascadeContext ctx, AddressCascadeLevel level)
    {
        var country = string.IsNullOrWhiteSpace(ctx.CountryName) ? "Chile" : ctx.CountryName.Trim();

        return level switch
        {
            AddressCascadeLevel.Country => country,
            AddressCascadeLevel.Region when !string.IsNullOrWhiteSpace(ctx.RegionName) =>
                $"{ctx.RegionName.Trim()}, {country}",
            AddressCascadeLevel.Comuna when !string.IsNullOrWhiteSpace(ctx.ComunaName) =>
                string.IsNullOrWhiteSpace(ctx.RegionName)
                    ? $"{ctx.ComunaName.Trim()}, {country}"
                    : $"{ctx.ComunaName.Trim()}, {ctx.RegionName.Trim()}, {country}",
            _ => null
        };
    }

    public static async Task<AddressCascadeResult?> ApplyAsync(
        IJSRuntime js,
        string mapElementId,
        AddressCascadeContext ctx,
        AddressCascadeLevel? targetLevel = null,
        bool resetManualAdjust = true,
        CancellationToken cancellationToken = default)
    {
        var level = targetLevel ?? ResolveLevel(ctx);
        if (level == AddressCascadeLevel.None)
        {
            return null;
        }

        if (resetManualAdjust)
        {
            try
            {
                await js.InvokeVoidAsync(ResetManualAdjustJs, cancellationToken, mapElementId);
            }
            catch (JSException)
            {
            }
        }

        var zoom = ZoomFor(level);
        var moveMarker = MovesMarker(level);
        var country = string.IsNullOrWhiteSpace(ctx.CountryName) ? "Chile" : ctx.CountryName;

        try
        {
            if (level == AddressCascadeLevel.Comuna
                && ctx.ComunaLatitude is double comunaLat
                && ctx.ComunaLongitude is double comunaLng)
            {
                await FocusAsync(js, mapElementId, comunaLat, comunaLng, zoom, moveMarker, forceMove: true, cancellationToken);
                return new AddressCascadeResult(comunaLat, comunaLng, level);
            }

            GeoCoordsDto? coords;
            if (level >= AddressCascadeLevel.Street)
            {
                var street = ctx.Street?.Trim();
                if (string.IsNullOrEmpty(street))
                {
                    return null;
                }

                coords = await js.InvokeAsync<GeoCoordsDto?>(
                    GeocodeStructuredJs,
                    cancellationToken,
                    street,
                    string.IsNullOrWhiteSpace(ctx.Number) ? null : ctx.Number.Trim(),
                    ctx.ComunaName,
                    ctx.RegionName,
                    country);
            }
            else
            {
                var query = BuildFreeTextQuery(ctx, level);
                if (string.IsNullOrWhiteSpace(query))
                {
                    return null;
                }

                coords = await js.InvokeAsync<GeoCoordsDto?>(GeocodeQueryJs, cancellationToken, query);
            }

            if (coords is null)
            {
                return null;
            }

            await FocusAsync(
                js,
                mapElementId,
                coords.Latitude,
                coords.Longitude,
                zoom,
                moveMarker,
                forceMove: moveMarker,
                cancellationToken);

            return new AddressCascadeResult(coords.Latitude, coords.Longitude, level);
        }
        catch (JSException)
        {
            return null;
        }
    }

    private static async Task FocusAsync(
        IJSRuntime js,
        string mapElementId,
        double latitude,
        double longitude,
        int zoom,
        bool moveMarker,
        bool forceMove,
        CancellationToken cancellationToken)
    {
        await js.InvokeVoidAsync(
            FocusMapJs,
            cancellationToken,
            mapElementId,
            latitude,
            longitude,
            zoom,
            moveMarker,
            forceMove);
    }

    private sealed record GeoCoordsDto(double Latitude, double Longitude);
}
