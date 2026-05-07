namespace RazorIdentity.Models.Api;

/// <summary>Contrato JSON esperado desde la API PYC (ajustar propiedades al contrato real).</summary>
public class PycPaisDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public string? Codigo { get; set; }
}

public class PycRegionDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public int PaisId { get; set; }
}

public class PycProyectoDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public int RegionId { get; set; }
    public string? Codigo { get; set; }
}
