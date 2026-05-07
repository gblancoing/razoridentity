using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using RazorIdentity.Configuration;

namespace RazorIdentity.Services;

public class PycApiClient : IPycApiClient
{
    private readonly HttpClient _http;
    private readonly PycApiSettings _settings;

    private static readonly JsonSerializerOptions _opts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private static readonly JsonSerializerOptions _pretty = new() { WriteIndented = true };

    /// <summary>POST importación: sin camelCase; respeta snake_case del PHP (<c>proyecto_id</c>, <c>rows</c>).</summary>
    private static readonly JsonSerializerOptions _importPostOpts = new()
    {
        PropertyNamingPolicy = null,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public PycApiClient(HttpClient http, IOptions<PycApiSettings> settings)
    {
        _http = http;
        _settings = settings.Value;
    }

    public async Task<string> GetRawAsync(string ruta, CancellationToken ct = default)
    {
        var response = await _http.GetAsync(ruta, ct);
        var json = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"HTTP {(int)response.StatusCode}: {json}");
        return PrettyPrint(json);
    }

    public async Task<string> PostRawAsync<TRequest>(string ruta, TRequest body, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync(ruta, body, _opts, ct);
        var json = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"HTTP {(int)response.StatusCode}: {json}");
        return PrettyPrint(json);
    }

    public async Task<List<T>> GetListAsync<T>(string ruta, CancellationToken ct = default)
    {
        var response = await _http.GetAsync(ruta, ct);
        var json = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"HTTP {(int)response.StatusCode}: {json}");
        var list = JsonSerializer.Deserialize<List<T>>(json, _opts);
        return list ?? new List<T>();
    }

    public async Task<T?> GetAsync<T>(string ruta, CancellationToken ct = default)
    {
        var response = await _http.GetAsync(ruta, ct);
        var json = await response.Content.ReadAsStringAsync(ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return default;
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"HTTP {(int)response.StatusCode}: {json}");
        return JsonSerializer.Deserialize<T>(json, _opts);
    }

    public async Task<JsonElement?> GetDatosFinancierosAsync(int proyectoId, string tabla, string? desde, string? hasta, CancellationToken ct = default)
    {
        string q;
        if (_settings.UsePhpEndpoints)
        {
            q = $"api/datos_financieros.php?proyecto_id={proyectoId}&tabla={Uri.EscapeDataString(tabla)}";
        }
        else
        {
            q = $"api/datos-financieros?proyecto_id={proyectoId}&tabla={Uri.EscapeDataString(tabla)}";
            if (!string.IsNullOrWhiteSpace(desde))
                q += $"&desde={Uri.EscapeDataString(desde)}";
            if (!string.IsNullOrWhiteSpace(hasta))
                q += $"&hasta={Uri.EscapeDataString(hasta)}";
        }

        var response = await _http.GetAsync(q, ct);
        var raw = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"HTTP {(int)response.StatusCode}: {raw}");

        using var doc = JsonDocument.Parse(raw);
        var root = doc.RootElement;
        if (root.ValueKind == JsonValueKind.Array)
            return root.Clone();
        if (root.TryGetProperty("datos", out var datos))
        {
            if (datos.ValueKind == JsonValueKind.Array)
                return datos.Clone();
            if (!string.IsNullOrEmpty(tabla) && datos.ValueKind == JsonValueKind.Object && datos.TryGetProperty(tabla, out var porTabla) && porTabla.ValueKind == JsonValueKind.Array)
                return porTabla.Clone();
        }

        return null;
    }

    public async Task<JsonElement?> GetAvFisicoDatosAsync(int proyectoId, string tabla, string? desde, string? hasta, CancellationToken ct = default)
    {
        if (!_settings.UsePhpEndpoints)
            return null;

        var t = tabla.Trim();
        if (string.IsNullOrEmpty(t))
            return null;

        var archivo = t.EndsWith(".php", StringComparison.OrdinalIgnoreCase) ? t : $"{t}.php";
        var q = $"api/{archivo}?proyecto_id={proyectoId}";
        if (!string.IsNullOrWhiteSpace(desde))
            q += $"&fecha_desde={Uri.EscapeDataString(desde.Trim())}";
        if (!string.IsNullOrWhiteSpace(hasta))
            q += $"&fecha_hasta={Uri.EscapeDataString(hasta.Trim())}";

        var response = await _http.GetAsync(q, ct);
        var raw = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"HTTP {(int)response.StatusCode}: {raw}");

        using var doc = JsonDocument.Parse(raw);
        var root = doc.RootElement;
        if (root.ValueKind == JsonValueKind.Array)
            return root.Clone();
        if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
            return data.Clone();
        if (root.TryGetProperty("datos", out var datos) && datos.ValueKind == JsonValueKind.Array)
            return datos.Clone();

        return null;
    }

    public async Task<string> PostImportacionAsync(string rutaRelativa, object body, CancellationToken ct = default)
    {
        var path = rutaRelativa.TrimStart('/');
        using var content = JsonContent.Create(body, options: _importPostOpts);
        var response = await _http.PostAsync(path, content, ct);
        var text = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"HTTP {(int)response.StatusCode}: {text}");
        return PrettyPrint(text);
    }

    private static string PrettyPrint(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(doc, _pretty);
        }
        catch { return json; }
    }
}
