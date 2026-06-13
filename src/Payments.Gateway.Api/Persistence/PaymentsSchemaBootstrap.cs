using Microsoft.EntityFrameworkCore;

namespace Payments.Gateway.Api.Persistence;

/// <summary>
/// Aplica al arranque el DDL idempotente de las tablas/columnas nuevas del gateway
/// (mismo contenido que "base de datos/04_payments.sql"), para que dev y prod
/// converjan sin intervención manual. Tolerante a permisos limitados: si el rol
/// de BD no puede crear objetos, solo se registra una advertencia.
/// </summary>
public static class PaymentsSchemaBootstrap
{
    private const string Ddl = """
        CREATE TABLE IF NOT EXISTS payments.subscriptions (
          id                 uuid PRIMARY KEY DEFAULT gen_random_uuid(),
          customer_id        text NOT NULL,
          customer_token_id  uuid NULL REFERENCES payments.customer_tokens(id) ON DELETE SET NULL,
          external_reference text NOT NULL,
          plan_name          text NULL,
          provider           text NOT NULL DEFAULT 'transbank',
          amount             numeric(14,2) NOT NULL,
          currency           text NOT NULL DEFAULT 'CLP',
          billing_interval   text NOT NULL DEFAULT 'monthly',
          status             text NOT NULL DEFAULT 'active' CHECK (status IN ('active','paused','past_due','cancelled','expired')),
          next_charge_at     timestamptz NULL,
          cancelled_at       timestamptz NULL,
          created_at         timestamptz NOT NULL DEFAULT now(),
          updated_at         timestamptz NOT NULL DEFAULT now()
        );

        CREATE INDEX IF NOT EXISTS ix_payments_subscriptions_status ON payments.subscriptions(status);
        CREATE INDEX IF NOT EXISTS ix_payments_subscriptions_customer ON payments.subscriptions(customer_id);
        CREATE INDEX IF NOT EXISTS ix_payments_subscriptions_external_ref ON payments.subscriptions(external_reference);

        CREATE TABLE IF NOT EXISTS payments.subscription_attempts (
          id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
          subscription_id uuid NOT NULL REFERENCES payments.subscriptions(id) ON DELETE CASCADE,
          intent_id       uuid NULL REFERENCES payments.payment_intents(id) ON DELETE SET NULL,
          charge_id       uuid NULL REFERENCES payments.charges(id) ON DELETE SET NULL,
          status          text NOT NULL,
          error_message   text NULL,
          attempted_at    timestamptz NOT NULL DEFAULT now()
        );

        CREATE INDEX IF NOT EXISTS ix_payments_subscription_attempts_subscription ON payments.subscription_attempts(subscription_id);

        ALTER TABLE payments.payment_intents ADD COLUMN IF NOT EXISTS review_status text NULL;
        ALTER TABLE payments.payment_intents ADD COLUMN IF NOT EXISTS review_note text NULL;
        ALTER TABLE payments.payment_intents ADD COLUMN IF NOT EXISTS core_notified_at timestamptz NULL;
        ALTER TABLE payments.payment_intents ADD COLUMN IF NOT EXISTS core_notify_attempts int NOT NULL DEFAULT 0;
        """;

    public static async Task ApplyAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("PaymentsSchemaBootstrap");

        try
        {
            await db.Database.ExecuteSqlRawAsync(Ddl, cancellationToken);
            logger.LogInformation("Esquema payments verificado/actualizado (subscriptions, subscription_attempts, columnas de operación).");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "No se pudo aplicar el bootstrap de esquema payments. " +
                "Aplica manualmente 'base de datos/04_payments.sql' si las tablas nuevas no existen.");
        }
    }
}
