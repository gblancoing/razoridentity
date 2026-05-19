BEGIN;

CREATE EXTENSION IF NOT EXISTS pgcrypto;

ALTER TABLE core.orders
    ADD COLUMN IF NOT EXISTS external_reference text,
    ADD COLUMN IF NOT EXISTS gross_amount numeric(14,2) NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS platform_fee_amount numeric(14,2) NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS net_amount numeric(14,2) NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS buyer_email text,
    ADD COLUMN IF NOT EXISTS buyer_name text;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_indexes
        WHERE schemaname = 'core'
          AND indexname = 'ix_orders_tenant_id_external_reference'
    ) THEN
        CREATE UNIQUE INDEX ix_orders_tenant_id_external_reference
            ON core.orders (tenant_id, external_reference)
            WHERE external_reference IS NOT NULL;
    END IF;
END $$;

CREATE TABLE IF NOT EXISTS core.sellers
(
    id uuid PRIMARY KEY,
    tenant_id uuid NOT NULL REFERENCES core.tenants(id) ON DELETE CASCADE,
    name text NOT NULL,
    email text NOT NULL,
    tax_id text NULL,
    is_active boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_sellers_tenant_id_email
    ON core.sellers (tenant_id, email);

ALTER TABLE core.payments
    ADD COLUMN IF NOT EXISTS seller_id uuid NULL REFERENCES core.sellers(id) ON DELETE SET NULL,
    ADD COLUMN IF NOT EXISTS order_id uuid NULL REFERENCES core.orders(id) ON DELETE SET NULL,
    ADD COLUMN IF NOT EXISTS transaction_amount numeric(14,2) NULL,
    ADD COLUMN IF NOT EXISTS paid_amount numeric(14,2) NULL,
    ADD COLUMN IF NOT EXISTS status_detail text NULL,
    ADD COLUMN IF NOT EXISTS mercadopago_payment_id text NULL,
    ADD COLUMN IF NOT EXISTS payment_method text NULL,
    ADD COLUMN IF NOT EXISTS raw_response_json jsonb NULL,
    ADD COLUMN IF NOT EXISTS idempotency_key text NULL,
    ADD COLUMN IF NOT EXISTS correlation_id text NULL,
    ADD COLUMN IF NOT EXISTS date_approved timestamptz NULL;

CREATE INDEX IF NOT EXISTS ix_payments_order_id
    ON core.payments (order_id);

CREATE INDEX IF NOT EXISTS ix_payments_seller_id
    ON core.payments (seller_id);

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_indexes
        WHERE schemaname = 'core'
          AND indexname = 'ix_payments_idempotency_key'
    ) THEN
        CREATE UNIQUE INDEX ix_payments_idempotency_key
            ON core.payments (idempotency_key)
            WHERE idempotency_key IS NOT NULL;
    END IF;
END $$;

CREATE TABLE IF NOT EXISTS core.seller_mercadopago_accounts
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    seller_id uuid NOT NULL REFERENCES core.sellers(id) ON DELETE CASCADE,
    mp_user_id text NULL,
    access_token_encrypted text NOT NULL,
    refresh_token_encrypted text NULL,
    token_expires_at timestamptz NULL,
    scope text NULL,
    connection_status text NOT NULL,
    connected_at timestamptz NULL,
    revoked_at timestamptz NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX IF NOT EXISTS ix_seller_mercadopago_accounts_seller_id
    ON core.seller_mercadopago_accounts (seller_id);

CREATE INDEX IF NOT EXISTS ix_seller_mercadopago_accounts_mp_user_id
    ON core.seller_mercadopago_accounts (mp_user_id);

CREATE TABLE IF NOT EXISTS core.seller_fee_configurations
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    seller_id uuid NOT NULL REFERENCES core.sellers(id) ON DELETE CASCADE,
    fixed_fee_amount numeric(14,2) NOT NULL DEFAULT 0,
    percentage_fee numeric(9,4) NOT NULL DEFAULT 0,
    is_active boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX IF NOT EXISTS ix_seller_fee_configurations_seller_id
    ON core.seller_fee_configurations (seller_id);

CREATE TABLE IF NOT EXISTS core.payment_fees
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    payment_id uuid NOT NULL REFERENCES core.payments(id) ON DELETE CASCADE,
    platform_fee_amount numeric(14,2) NOT NULL,
    mercadopago_fee_amount numeric(14,2) NULL,
    net_to_seller_amount numeric(14,2) NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX IF NOT EXISTS ix_payment_fees_payment_id
    ON core.payment_fees (payment_id);

CREATE TABLE IF NOT EXISTS core.webhook_events
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    topic text NOT NULL,
    action text NULL,
    resource_id text NULL,
    payload_json jsonb NOT NULL,
    signature_valid boolean NOT NULL DEFAULT false,
    processed boolean NOT NULL DEFAULT false,
    processed_at timestamptz NULL,
    error_message text NULL,
    created_at timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_webhook_events_topic_action_resource_id_created_at
    ON core.webhook_events (topic, action, resource_id, created_at);

CREATE TABLE IF NOT EXISTS core.payment_status_history
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    payment_id uuid NOT NULL REFERENCES core.payments(id) ON DELETE CASCADE,
    previous_status text NULL,
    new_status text NOT NULL,
    detail text NULL,
    raw_payload_json jsonb NULL,
    created_at timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_payment_status_history_payment_id_created_at
    ON core.payment_status_history (payment_id, created_at);

CREATE TABLE IF NOT EXISTS core.audit_logs
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    actor text NOT NULL,
    action text NOT NULL,
    entity_type text NOT NULL,
    entity_id text NOT NULL,
    data_json jsonb NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_audit_logs_entity_type_entity_id_created_at
    ON core.audit_logs (entity_type, entity_id, created_at);

INSERT INTO core.sellers (id, tenant_id, name, email, tax_id, is_active, created_at, updated_at)
SELECT p.id,
       p.tenant_id,
       p.name,
       COALESCE(NULLIF(p.email, ''), 'seller-' || replace(p.id::text, '-', '') || '@comunaclic.cl'),
       p.rut,
       true,
       COALESCE(p.created_at, now()),
       COALESCE(p.updated_at, now())
FROM core.partners p
LEFT JOIN core.sellers s ON s.id = p.id
WHERE s.id IS NULL;

COMMIT;
