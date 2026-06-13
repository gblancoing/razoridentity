namespace ComunaClick.Api.Modules.Orders;

/// <summary>
/// Máquina de estados explícita para órdenes. Cualquier transición fuera de este
/// grafo se rechaza; "cancelled" es terminal.
/// </summary>
public static class OrderStatusMachine
{
    public const string PaymentPending = "payment_pending";
    public const string Processing = "processing";
    public const string Paid = "paid";
    public const string Cancelled = "cancelled";

    private static readonly Dictionary<string, string[]> AllowedTransitions = new(StringComparer.OrdinalIgnoreCase)
    {
        [PaymentPending] = [Processing, Paid, Cancelled],
        [Processing] = [Paid, Cancelled],
        [Paid] = [Cancelled],
        [Cancelled] = []
    };

    public static string Normalize(string? status)
    {
        var normalized = status?.Trim().ToLowerInvariant() ?? string.Empty;
        return normalized switch
        {
            "canceled" => Cancelled,
            "approved" => Paid,
            _ => normalized
        };
    }

    public static bool IsKnownStatus(string? status)
        => AllowedTransitions.ContainsKey(Normalize(status));

    public static bool CanTransition(string? from, string? to, out string? error)
    {
        var source = Normalize(from);
        var target = Normalize(to);

        if (!AllowedTransitions.ContainsKey(target))
        {
            error = $"Estado de orden no válido: \"{to}\".";
            return false;
        }

        if (string.Equals(source, target, StringComparison.OrdinalIgnoreCase))
        {
            error = null;
            return true;
        }

        // Estados legados/desconocidos en datos antiguos: permitir solo cancelación.
        if (!AllowedTransitions.TryGetValue(source, out var targets))
        {
            if (string.Equals(target, Cancelled, StringComparison.OrdinalIgnoreCase))
            {
                error = null;
                return true;
            }

            error = $"La orden tiene un estado no reconocido (\"{from}\") y solo puede cancelarse.";
            return false;
        }

        if (targets.Contains(target, StringComparer.OrdinalIgnoreCase))
        {
            error = null;
            return true;
        }

        error = $"Transición de estado no permitida: \"{source}\" → \"{target}\".";
        return false;
    }
}
