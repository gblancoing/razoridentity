namespace RazorIdentity.Models.Api;

public class EmpresaColaboradoraApi
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public int CentroCostoId { get; set; }
    public string? Rut { get; set; }
    public string? Direccion { get; set; }
    public string? Ubicacion { get; set; }
    public DateTime? FechaInicioContrato { get; set; }
    public DateTime? FechaTerminoEsperadaContrato { get; set; }
    public string? DescripcionServicios { get; set; }
    public string? Email { get; set; }
    public string? Telefono { get; set; }
}
