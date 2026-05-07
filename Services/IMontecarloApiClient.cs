namespace RazorIdentity.Services;

/// <summary>Cliente HTTP para la API Monte Carlo (localhost:5080).</summary>
public interface IMontecarloApiClient
{
    Task<string> PostRawAsync<TRequest>(string ruta, TRequest body, CancellationToken ct = default);
    Task<string> GetRawAsync(string ruta, CancellationToken ct = default);
}
