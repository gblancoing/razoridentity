namespace RazorIdentity.Models.Api;

public class AppApi
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public string? DescripcionApp { get; set; }
    public string? ImagenApp { get; set; }
}
