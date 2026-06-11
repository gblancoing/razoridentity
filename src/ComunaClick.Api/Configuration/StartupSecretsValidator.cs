namespace ComunaClick.Api.Configuration;

/// <summary>
/// Valida en producción que los secretos críticos estén presentes y no sean placeholders.
/// Si falta alguno, aborta el arranque para no exponer la app con claves inseguras.
/// </summary>
public static class StartupSecretsValidator
{
    private static readonly string[] KnownPlaceholders =
    [
        "CHANGE_ME",
        "LOCAL_DEV_JWT_SIGNING_KEY_MIN_32_CHARS!!"
    ];

    public static void ValidateOrThrow(IConfiguration configuration, IHostEnvironment environment)
    {
        if (!environment.IsProduction())
        {
            return;
        }

        var errors = new List<string>();

        var jwtKey = configuration["Jwt:SigningKey"];
        if (IsMissingOrPlaceholder(jwtKey) || jwtKey!.Trim().Length < 32)
        {
            errors.Add("Jwt:SigningKey debe ser un secreto fuerte (≥ 32 caracteres) y no un placeholder.");
        }

        var webhookKey = configuration["Payments:InternalWebhookKey"]
            ?? configuration["ComunaClic:InternalWebhookKey"];
        if (IsMissingOrPlaceholder(webhookKey) || webhookKey!.Trim().Length < 32)
        {
            errors.Add("Payments:InternalWebhookKey debe ser un secreto fuerte (≥ 32 caracteres) y no un placeholder.");
        }

        ValidateMercadoPago(configuration, errors);

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "Configuración insegura en producción. Defina estos secretos por variables de entorno/secret manager:"
                + Environment.NewLine + " - " + string.Join(Environment.NewLine + " - ", errors));
        }
    }

    private static void ValidateMercadoPago(IConfiguration configuration, List<string> errors)
    {
        // El marketplace de MercadoPago es obligatorio para cobrar en línea. Acepta override
        // por variables de entorno (MP_*) además de la sección Marketplace:MercadoPago.
        var checks = new (string Key, string? EnvOverride)[]
        {
            ("Marketplace:MercadoPago:ClientId", configuration["MP_CLIENT_ID"]),
            ("Marketplace:MercadoPago:ClientSecret", configuration["MP_CLIENT_SECRET"]),
            ("Marketplace:MercadoPago:WebhookSecret", configuration["MP_WEBHOOK_SECRET"]),
            ("Marketplace:MercadoPago:EncryptionKey", configuration["ENCRYPTION_KEY"])
        };

        foreach (var (key, envOverride) in checks)
        {
            var value = configuration[key];
            if (IsMissingOrPlaceholder(value) && IsMissingOrPlaceholder(envOverride))
            {
                errors.Add($"{key} (o su variable de entorno) no puede estar vacío en producción.");
            }
        }
    }

    private static bool IsMissingOrPlaceholder(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var trimmed = value.Trim();
        return KnownPlaceholders.Any(p => string.Equals(p, trimmed, StringComparison.OrdinalIgnoreCase))
            || trimmed.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase);
    }
}
