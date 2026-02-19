namespace RazorIdentity.Models.Api;

/// <summary>
/// Modelo que coincide con la respuesta del endpoint GET /WeatherForecast de Rit_Api.
/// </summary>
public class WeatherForecast
{
    public DateTime Date { get; set; }
    public int TemperatureC { get; set; }
    public int TemperatureF { get; set; }
    public string? Summary { get; set; }
}
