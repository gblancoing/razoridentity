using System.Text.Json;

namespace RazorIdentity.Services;

/// <summary>Cliente HTTP para la API PYC / PMO (Planning &amp; Control), mismo estilo que <see cref="IMontecarloApiClient"/>.</summary>
public interface IPycApiClient
{
    Task<string> GetRawAsync(string ruta, CancellationToken ct = default);
    Task<string> PostRawAsync<TRequest>(string ruta, TRequest body, CancellationToken ct = default);

    Task<List<T>> GetListAsync<T>(string ruta, CancellationToken ct = default);
    Task<T?> GetAsync<T>(string ruta, CancellationToken ct = default);

    /// <summary>GET datos financieros: respuesta <c>{ success, datos }</c> o un array JSON.</summary>
    Task<JsonElement?> GetDatosFinancierosAsync(int proyectoId, string tabla, string? desde, string? hasta, CancellationToken ct = default);

    /// <summary>GET avance físico (<c>av_fisico_*.php</c>): respuesta <c>{ success, data }</c> o array.</summary>
    Task<JsonElement?> GetAvFisicoDatosAsync(int proyectoId, string tabla, string? desde, string? hasta, CancellationToken ct = default);

    /// <summary>POST importación con cuerpo alineado al PHP (<c>proyecto_id</c>, <c>rows</c>); no usa camelCase en propiedades.</summary>
    Task<string> PostImportacionAsync(string rutaRelativa, object body, CancellationToken ct = default);
}
