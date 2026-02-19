namespace RazorIdentity.Services;

/// <summary>
/// Cliente HTTP para llamar a Rit_Api (CRUD: GET lista, GET por id, POST, PUT, DELETE).
/// </summary>
public interface IRitApiClient
{
    Task<List<T>> GetListAsync<T>(string ruta, CancellationToken ct = default);
    Task<T?> GetAsync<T>(string ruta, CancellationToken ct = default);
    Task<T?> PostAsync<TRequest, T>(string ruta, TRequest body, CancellationToken ct = default);
    Task<T?> PutAsync<TRequest, T>(string ruta, TRequest body, CancellationToken ct = default);
    Task DeleteAsync(string ruta, CancellationToken ct = default);
}
