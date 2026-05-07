namespace RazorIdentity.Models.TallerCostos
{
    /// <summary>
    /// Resultado guardado del análisis Monte Carlo por contrato (Bloque F).
    /// Se sobreescribe cada vez que el usuario ejecuta "Generar Análisis MonteCarlo".
    /// </summary>
    public class TallerMcResultado
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid ContratoId { get; set; }
        public DateTime FechaCalculo { get; set; } = DateTime.UtcNow;

        public double P10 { get; set; }
        public double P50 { get; set; }
        public double P80 { get; set; }
        public double P90 { get; set; }
        public double Media { get; set; }
        public double DesvStd { get; set; }
        public int Iteraciones { get; set; }

        /// <summary>JSON array con la media MC por ítem (KUS$), en orden.</summary>
        public string ItemMediasJson { get; set; } = "[]";

        /// <summary>JSON { labels: number[], counts: number[] } para el histograma.</summary>
        public string HistogramaJson { get; set; } = "{}";

        /// <summary>JSON array de { x, y } para la curva CDF.</summary>
        public string CdfJson { get; set; } = "[]";

        public TallerContrato Contrato { get; set; } = null!;
    }
}
