-- =========================================================
-- 04_payments.sql
-- Ejecutar conectado a la DB: payments_db
-- =========================================================

-- Extensiones
CREATE EXTENSION IF NOT EXISTS "pgcrypto";
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- Schema + permisos
CREATE SCHEMA IF NOT EXISTS payments AUTHORIZATION payments_app_user;

GRANT USAGE, CREATE ON SCHEMA payments TO payments_app_user;
ALTER ROLE payments_app_user IN DATABASE payments_db SET search_path = payments, public;

ALTER DEFAULT PRIVILEGES IN SCHEMA payments
GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO payments_app_user;

ALTER DEFAULT PRIVILEGES IN SCHEMA payments
GRANT USAGE, SELECT, UPDATE ON SEQUENCES TO payments_app_user;

-- =========================
-- TABLAS (Gateway independiente)
-- =========================

CREATE TABLE IF NOT EXISTS payments.payment_intents (
  id                 uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  external_reference  text NOT NULL, -- order:{uuid} | booking:{uuid} | subscription:{uuid}
  amount              numeric(14,2) NOT NULL,
  currency            text NOT NULL DEFAULT 'CLP',
  status              text NOT NULL CHECK (status IN ('pending','approved','rejected','cancelled','expired')),
  provider            text NOT NULL DEFAULT 'transbank',
  provider_token      text NULL,   -- token_ws
  authorization_code  text NULL,
  raw_response        jsonb NOT NULL DEFAULT '{}'::jsonb,
  created_at          timestamptz NOT NULL DEFAULT now(),
  updated_at          timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_payments_intents_status
  ON payments.payment_intents(status);

CREATE INDEX IF NOT EXISTS ix_payments_intents_external_ref
  ON payments.payment_intents(external_reference);

-- Idempotencia de callbacks/eventos
CREATE TABLE IF NOT EXISTS payments.provider_events (
  id                uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  provider_event_id  text NOT NULL,
  intent_id          uuid NOT NULL REFERENCES payments.payment_intents(id) ON DELETE CASCADE,
  event_type         text NOT NULL, -- callback|commit|status_change|charge|enrollment
  payload            jsonb NOT NULL DEFAULT '{}'::jsonb,
  received_at        timestamptz NOT NULL DEFAULT now(),
  UNIQUE (provider_event_id)
);

CREATE INDEX IF NOT EXISTS ix_payments_provider_events_intent
  ON payments.provider_events(intent_id);

-- Oneclick: tokens (inscripciones)
CREATE TABLE IF NOT EXISTS payments.customer_tokens (
  id            uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  customer_id    text NOT NULL,  -- id lógico del cliente (core)
  provider_ref   text NOT NULL,  -- tbk_user/username u otro identificador requerido para cargos
  status         text NOT NULL CHECK (status IN ('active','revoked','failed')),
  raw_response   jsonb NOT NULL DEFAULT '{}'::jsonb,
  created_at     timestamptz NOT NULL DEFAULT now(),
  revoked_at     timestamptz NULL,
  UNIQUE (customer_id, provider_ref)
);

CREATE INDEX IF NOT EXISTS ix_payments_customer_tokens_customer
  ON payments.customer_tokens(customer_id);

CREATE INDEX IF NOT EXISTS ix_payments_customer_tokens_status
  ON payments.customer_tokens(status);

-- Oneclick charges
CREATE TABLE IF NOT EXISTS payments.charges (
  id                 uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  customer_token_id   uuid NOT NULL REFERENCES payments.customer_tokens(id) ON DELETE RESTRICT,
  intent_id           uuid NOT NULL REFERENCES payments.payment_intents(id) ON DELETE CASCADE,
  amount              numeric(14,2) NOT NULL,
  currency            text NOT NULL DEFAULT 'CLP',
  status              text NOT NULL CHECK (status IN ('pending','approved','rejected','cancelled')),
  provider_ref        text NULL,
  authorization_code  text NULL,
  raw_response        jsonb NOT NULL DEFAULT '{}'::jsonb,
  created_at          timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_payments_charges_intent
  ON payments.charges(intent_id);

CREATE INDEX IF NOT EXISTS ix_payments_charges_status
  ON payments.charges(status);

-- updated_at trigger (opcional)
CREATE OR REPLACE FUNCTION payments.set_updated_at()
RETURNS trigger AS $$
BEGIN
  NEW.updated_at = now();
  RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_payments_intents_updated_at ON payments.payment_intents;
CREATE TRIGGER trg_payments_intents_updated_at
BEFORE UPDATE ON payments.payment_intents
FOR EACH ROW EXECUTE FUNCTION payments.set_updated_at();
