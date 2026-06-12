using Npgsql;

namespace ComunaClick.Api.Persistence;

/// <summary>
/// Aplica migraciones SQL idempotentes cuando faltan tablas o columnas (p. ej. buzón).
/// </summary>
public static class DatabaseSchemaBootstrap
{
    private static readonly (string Schema, string Table, string MigrationFile, string? Column)[] PendingChecks =
    [
        ("core", "products", "2026-04-23_product_image_url.sql", "image_url"),
        ("core", "inbox_threads", "20260525_inbox_messages.sql", null),
        ("core", "service_professionals", "20260526_service_catalog_type_b.sql", null),
        ("core", "service_images", "20260527_service_images_address.sql", null),
        ("core", "partners", "20260528_partner_professional_storefront.sql", "banner_url"),
        ("core", "partner_catalog_categories", "20260529_commerce_type_a.sql", null),
        ("core", "partners", "20260530_partner_bank_account.sql", "bank_account_number"),
        ("core", "partners", "20260531_partner_logo_url.sql", "logo_url"),
        ("core", "partners", "20260601_profile_web_links.sql", "website_url"),
        ("core", "services", "20260602_service_geo_coordinates.sql", "latitude"),
        ("core", "sellers", "2026-04-23_marketplace_mercadopago.sql", null),
        ("core", "professionals", "20260609_professional_photo_url.sql", "profile_photo_url"),
        ("core", "professionals", "20260609_professional_stats_certifications.sql", "profile_view_count"),
        ("core", "professional_follows", "20260609_professional_follows.sql", null),
        ("core", "orders", "20260610_order_inventory_fulfilled.sql", "inventory_fulfilled_at"),
        ("core", "notification_outbox", "20260611_notification_outbox.sql", null),
        ("core", "partner_catalog_categories", "20260611_partner_catalog_subcategories.sql", "parent_id"),
        ("core", "couriers", "20260612_delivery_tracking.sql", null),
        ("core", "delivery_settlements", "20260613_delivery_settlements.sql", null),
        ("core", "couriers", "20260613_delivery_settlements.sql", "kind"),
        ("core", "partners", "20260614_partner_preferred_delivery_provider.sql", "preferred_delivery_provider_id")
    ];

    public static async Task ApplyPendingAsync(string? connectionString, IHostEnvironment env, ILogger logger, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        foreach (var (schema, table, fileName, column) in PendingChecks)
        {
            var needsMigration = column is null
                ? !await TableExistsAsync(connection, schema, table, cancellationToken)
                : !await ColumnExistsAsync(connection, schema, table, column, cancellationToken);

            if (!needsMigration)
            {
                continue;
            }

            var path = ResolveMigrationPath(env, fileName);
            if (path is null || !File.Exists(path))
            {
                logger.LogWarning("Missing SQL migration file for {Schema}.{Table}: {File}", schema, table, fileName);
                continue;
            }

            logger.LogInformation("Applying SQL migration {File} ({Schema}.{Table})…", fileName, schema, table);
            var sql = await File.ReadAllTextAsync(path, cancellationToken);
            await using var cmd = new NpgsqlCommand(sql, connection) { CommandTimeout = 120 };
            await cmd.ExecuteNonQueryAsync(cancellationToken);
            logger.LogInformation("Migration {File} applied.", fileName);
        }
    }

    private static async Task<bool> TableExistsAsync(NpgsqlConnection connection, string schema, string table, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1
                FROM information_schema.tables
                WHERE table_schema = @schema
                  AND table_name = @table
            );
            """;

        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("schema", schema);
        cmd.Parameters.AddWithValue("table", table);
        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result is bool exists && exists;
    }

    private static async Task<bool> ColumnExistsAsync(
        NpgsqlConnection connection,
        string schema,
        string table,
        string column,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1
                FROM information_schema.columns
                WHERE table_schema = @schema
                  AND table_name = @table
                  AND column_name = @column
            );
            """;

        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("schema", schema);
        cmd.Parameters.AddWithValue("table", table);
        cmd.Parameters.AddWithValue("column", column);
        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result is bool exists && exists;
    }

    private static string? ResolveMigrationPath(IHostEnvironment env, string fileName)
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "migrations", fileName),
            Path.Combine(env.ContentRootPath, "migrations", fileName),
            Path.Combine(env.ContentRootPath, "..", "..", "..", "infra", "migrations", fileName),
            Path.Combine(env.ContentRootPath, "..", "..", "infra", "migrations", fileName)
        };

        foreach (var candidate in candidates)
        {
            var full = Path.GetFullPath(candidate);
            if (File.Exists(full))
            {
                return full;
            }
        }

        return null;
    }
}
