-- Outbox de notificaciones de órdenes/reservas.
-- El envío SMTP/WhatsApp se saca del request: se encola aquí dentro de la transacción
-- del evento y un worker lo procesa con reintentos y backoff.
CREATE TABLE IF NOT EXISTS core.notification_outbox (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       uuid NOT NULL,
    channel         text NOT NULL DEFAULT 'email',
    kind            text NOT NULL,
    reference_id    uuid NULL,
    recipient       text NULL,
    subject         text NULL,
    body            text NOT NULL DEFAULT '',
    payload_json    jsonb NULL,
    status          text NOT NULL DEFAULT 'pending',
    attempts        integer NOT NULL DEFAULT 0,
    max_attempts    integer NOT NULL DEFAULT 5,
    next_attempt_at timestamptz NOT NULL DEFAULT now(),
    last_error      text NULL,
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now(),
    sent_at         timestamptz NULL
);

-- El worker barre por estado pendiente y fecha de próximo intento.
CREATE INDEX IF NOT EXISTS ix_notification_outbox_pending
    ON core.notification_outbox (status, next_attempt_at);

-- Idempotencia/consultas por evento relacionado.
CREATE INDEX IF NOT EXISTS ix_notification_outbox_reference
    ON core.notification_outbox (reference_id, kind);
