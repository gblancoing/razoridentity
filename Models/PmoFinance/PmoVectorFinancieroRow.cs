namespace RazorIdentity.Models.PmoFinance;

/// <summary>Fila estándar de vectores (Real/V0/NPC/API, parcial o acumulado). Tabla por subtipo (TPC).</summary>
public abstract class PmoVectorFinancieroRow
{
    public int Id { get; set; }
    public int ProyectoId { get; set; }
    public string CentroCosto { get; set; } = "";
    public DateOnly Periodo { get; set; }
    public string Tipo { get; set; } = "";
    public string CatVp { get; set; } = "";
    public string DetalleFactorial { get; set; } = "";
    public decimal Monto { get; set; }
}

public class PmoRealParcial : PmoVectorFinancieroRow;
public class PmoRealAcumulado : PmoVectorFinancieroRow;
public class PmoV0Parcial : PmoVectorFinancieroRow;
public class PmoV0Acumulada : PmoVectorFinancieroRow;
public class PmoNpcParcial : PmoVectorFinancieroRow;
public class PmoNpcAcumulado : PmoVectorFinancieroRow;
public class PmoApiParcial : PmoVectorFinancieroRow;
public class PmoApiAcumulada : PmoVectorFinancieroRow;
