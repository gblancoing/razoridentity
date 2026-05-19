using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.JSInterop;

namespace ComunaClick.SharedUI.Services;

/// <summary>
/// Persiste favoritos por correo de usuario en localStorage (JSON). Pensado para sincronizarse
/// luego con API; por ahora permite mostrar en /services-near lo guardado desde la app.
/// </summary>
public sealed class FavoritesLocalStore(IJSRuntime js)
{
    private const string VersionProperty = "version";
    private const int CurrentVersion = 1;

    public async Task<FavoritesDataRoot> LoadRootAsync(CancellationToken cancellationToken = default)
    {
        string? raw;
        try
        {
            raw = await js.InvokeAsync<string?>("comunaclic.getFavoritesData", cancellationToken);
        }
        catch (JSException)
        {
            return new FavoritesDataRoot();
        }

        if (string.IsNullOrWhiteSpace(raw))
        {
            return new FavoritesDataRoot();
        }

        try
        {
            return JsonSerializer.Deserialize<FavoritesDataRoot>(raw) ?? new FavoritesDataRoot();
        }
        catch
        {
            return new FavoritesDataRoot();
        }
    }

    public async Task<UserFavoritesBundle> GetForUserAsync(string? email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return new UserFavoritesBundle();
        }

        var root = await LoadRootAsync(cancellationToken);
        var key = email.Trim().ToLowerInvariant();
        if (root.Users.TryGetValue(key, out var bundle))
        {
            return bundle;
        }

        return new UserFavoritesBundle();
    }
}

public sealed class FavoritesDataRoot
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("users")]
    public Dictionary<string, UserFavoritesBundle> Users { get; set; } = new();
}

public sealed class UserFavoritesBundle
{
    [JsonPropertyName("places")]
    public List<FavoritePlaceItem> Places { get; set; } = [];

    [JsonPropertyName("businesses")]
    public List<FavoriteBusinessItem> Businesses { get; set; } = [];
}

public sealed class FavoritePlaceItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("detail")]
    public string? Detail { get; set; }
}

public sealed class FavoriteBusinessItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("comuna")]
    public string? Comuna { get; set; }

    /// <summary>Ruta de la ficha, p. ej. /buyer/detail/partner/{id}</summary>
    [JsonPropertyName("href")]
    public string Href { get; set; } = string.Empty;
}
