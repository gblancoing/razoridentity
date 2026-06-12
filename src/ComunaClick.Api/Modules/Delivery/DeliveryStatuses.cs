namespace ComunaClick.Api.Modules.Delivery;

/// <summary>Tipos de entrega de una orden.</summary>
public static class DeliveryTypes
{
    public const string Pickup = "pickup";
    public const string Delivery = "delivery";
}

/// <summary>
/// Máquina de estados del envío (patrón de OrderStatusMachine). Cualquier
/// transición fuera de este grafo se rechaza; "delivered" y "canceled" son
/// terminales y además revocan el token del repartidor.
/// </summary>
public static class DeliveryStatuses
{
    public const string Pending = "pending";
    public const string Assigned = "assigned";
    public const string PickedUp = "picked_up";
    public const string InTransit = "in_transit";
    public const string Delivered = "delivered";
    public const string Canceled = "canceled";

    private static readonly Dictionary<string, string[]> AllowedTransitions = new(StringComparer.OrdinalIgnoreCase)
    {
        [Pending] = [Assigned, Canceled],
        [Assigned] = [PickedUp, InTransit, Canceled],
        [PickedUp] = [InTransit, Delivered, Canceled],
        [InTransit] = [Delivered, Canceled],
        [Delivered] = [],
        [Canceled] = []
    };

    public static string Normalize(string? status)
        => status?.Trim().ToLowerInvariant() switch
        {
            "cancelled" => Canceled,
            { } value => value,
            _ => string.Empty
        };

    public static bool IsKnownStatus(string? status)
        => AllowedTransitions.ContainsKey(Normalize(status));

    /// <summary>True si el envío sigue activo (el repartidor puede reportar GPS).</summary>
    public static bool IsActive(string? status)
    {
        var normalized = Normalize(status);
        return normalized is Pending or Assigned or PickedUp or InTransit;
    }

    public static bool CanTransition(string? from, string? to, out string? error)
    {
        var source = Normalize(from);
        var target = Normalize(to);

        if (!AllowedTransitions.ContainsKey(target))
        {
            error = $"Estado de envío no válido: \"{to}\".";
            return false;
        }

        if (string.Equals(source, target, StringComparison.OrdinalIgnoreCase))
        {
            error = null;
            return true;
        }

        if (!AllowedTransitions.TryGetValue(source, out var targets))
        {
            error = $"El envío tiene un estado no reconocido (\"{from}\").";
            return false;
        }

        if (targets.Contains(target, StringComparer.OrdinalIgnoreCase))
        {
            error = null;
            return true;
        }

        error = $"Transición de envío no permitida: \"{source}\" → \"{target}\".";
        return false;
    }
}
