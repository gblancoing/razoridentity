namespace ComunaClick.Api.Modules.Orders;

public sealed class OrderTrackingOptions
{
    public const string SectionName = "Orders";

    /// <summary>Clave para firmar los tokens de seguimiento. Si está vacía se usa Jwt:SigningKey.</summary>
    public string TrackingTokenSecret { get; set; } = string.Empty;

    /// <summary>Vigencia del token de seguimiento (días).</summary>
    public int TrackingTokenTtlDays { get; set; } = 30;

    /// <summary>
    /// Permite (temporalmente) acceder a la orden pública con el par id+customerId de los
    /// enlaces antiguos. Debe ponerse en false en producción una vez migrado el frontend.
    /// </summary>
    public bool AllowLegacyPublicAccess { get; set; } = true;
}
