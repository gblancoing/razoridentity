namespace ComunaClick.SharedUI.Services;

public static class GuestCheckoutDeliveryHelper
{
    public const string ModeHome = "home";
    public const string ModeFree = "free";
    public const string ModePayOnDelivery = "pay_on_delivery";

    public static string? DetectSuggestedMode(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        var text = description.ToLowerInvariant();
        if (text.Contains("envío gratis") || text.Contains("envio gratis") || text.Contains("envío gratuito") ||
            text.Contains("retiro en") || text.Contains("retiro gratis"))
        {
            return ModeFree;
        }

        if (text.Contains("envío por pagar") || text.Contains("envio por pagar") ||
            text.Contains("pago en destino") || text.Contains("contra entrega") ||
            text.Contains("envío a pagar"))
        {
            return ModePayOnDelivery;
        }

        return null;
    }

    public static bool RequiresAddress(string mode)
        => mode is ModeHome or ModePayOnDelivery;

    public static bool Validate(string mode, string? address, out string? errorKey)
    {
        errorKey = null;
        if (!RequiresAddress(mode))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(address) || address.Trim().Length < 4)
        {
            errorKey = "buyer.checkout.delivery.addressRequired";
            return false;
        }

        return true;
    }

    public static string? FormatDeliveryAddress(string mode, string? address)
    {
        var trimmed = string.IsNullOrWhiteSpace(address) ? null : address.Trim();
        return mode switch
        {
            ModeFree => string.IsNullOrWhiteSpace(trimmed)
                ? "[Envío gratis / retiro] A coordinar con el negocio."
                : $"[Envío gratis / retiro] {trimmed}",
            ModePayOnDelivery => string.IsNullOrWhiteSpace(trimmed)
                ? "[Envío por pagar] A coordinar dirección con el negocio."
                : $"[Envío por pagar] {trimmed}",
            _ => trimmed
        };
    }
}
