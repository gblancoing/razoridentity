namespace RazorIdentity.Services;

/// <summary>
/// Cliente HTTP para API_Ritweb (eventos RIT: alertas, inspecciones, sectores, tipos de evento, etc.).
/// </summary>
public interface IApiRitwebClient
{
    Task<List<T>> GetListAsync<T>(string ruta, CancellationToken ct = default);
    Task<T?> GetAsync<T>(string ruta, CancellationToken ct = default);
    Task<T?> PostAsync<TRequest, T>(string ruta, TRequest body, CancellationToken ct = default);
    Task<T?> PostMultipartAsync<T>(string ruta, MultipartFormDataContent content, CancellationToken ct = default);
    Task<T?> PutAsync<TRequest, T>(string ruta, TRequest body, CancellationToken ct = default);
    Task PatchAsync(string ruta, object body, CancellationToken ct = default);
    Task DeleteAsync(string ruta, CancellationToken ct = default);
    /// <summary>Obtiene el contenido binario de una ruta (p. ej. descarga de adjunto).</summary>
    Task<byte[]?> GetByteArrayAsync(string ruta, CancellationToken ct = default);
}
